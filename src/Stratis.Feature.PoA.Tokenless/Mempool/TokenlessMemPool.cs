using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using static Stratis.Bitcoin.Features.MemoryPool.TxMempool;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    /// <summary>
    /// Memory pool of pending transactions.
    /// </summary>
    /// <remarks>
    ///
    /// TxMempool stores valid-according-to-the-current-best-chain transactions
    /// that may be included in the next block.
    ///
    /// Transactions are added when they are seen on the network(or created by the
    /// local node), but not all transactions seen are added to the pool.For
    /// example, the following new transactions will not be added to the mempool:
    /// - a transaction which doesn't make the mimimum fee requirements.
    /// - a new transaction that double-spends an input of a transaction already in
    /// the pool where the new transaction does not meet the Replace-By-Fee
    /// requirements as defined in BIP 125.
    /// - a non-standard transaction.
    ///
    /// <see cref="TxMempool.MapTx"/>, and <see cref="TxMempoolEntry"/> bookkeeping:
    ///
    /// <see cref="MapTx"/> is a collection that sorts the mempool on 4 criteria:
    /// - transaction hash
    /// - feerate[we use max(feerate of tx, feerate of Transaction with all descendants)]
    /// - time in mempool
    /// - mining score (feerate modified by any fee deltas from PrioritiseTransaction)
    ///
    /// Note: the term "descendant" refers to in-mempool transactions that depend on
    /// this one, while "ancestor" refers to in-mempool transactions that a given
    /// transaction depends on.
    ///
    /// In order for the feerate sort to remain correct, we must update transactions
    /// in the mempool when new descendants arrive. To facilitate this, we track
    /// the set of in-mempool direct parents and direct children in <see cref="mapLinks.Within"/>
    /// each TxMempoolEntry, we track the size and fees of all descendants.
    ///
    /// Usually when a new transaction is added to the mempool, it has no in-mempool
    /// children(because any such children would be an orphan).  So in
    /// <see cref="AddUnchecked(uint256, TxMempoolEntry, bool)"/>, we:
    /// - update a new entry's setMemPoolParents to include all in-mempool parents
    /// - update the new entry's direct parents to include the new tx as a child
    /// - update all ancestors of the transaction to include the new tx's size/fee
    ///
    /// When a transaction is removed from the mempool, we must:
    /// - update all in-mempool parents to not track the tx in setMemPoolChildren
    /// - update all ancestors to not include the tx's size/fees in descendant state
    /// - update all in-mempool children to not include it as a parent
    ///
    /// These happen in <see cref="UpdateForRemoveFromMempool(TxMempool.SetEntries, bool)"/>.
    /// (Note that when removing a
    /// transaction along with its descendants, we must calculate that set of
    /// transactions to be removed before doing the removal, or else the mempool can
    /// be in an inconsistent state where it's impossible to walk the ancestors of
    /// a transaction.)
    ///
    /// In the event of a reorg, the assumption that a newly added tx has no
    /// in-mempool children is false.  In particular, the mempool is in an
    /// inconsistent state while new transactions are being added, because there may
    /// be descendant transactions of a tx coming from a disconnected block that are
    /// unreachable from just looking at transactions in the mempool(the linking
    /// transactions may also be in the disconnected block, waiting to be added).
    /// Because of this, there's not much benefit in trying to search for in-mempool
    /// children in <see cref="AddUnchecked(uint256, TxMempoolEntry, SetEntries, bool)"/>.
    /// Instead, in the special case of transactions
    /// being added from a disconnected block, we require the caller to clean up the
    /// state, to account for in-mempool, out-of-block descendants for all the
    /// in-block transactions by calling <see cref="AddTransactionsUpdated(int)"/>.  Note that
    /// until this is called, the mempool state is not consistent, and in particular
    /// <see cref="mapLinks"/> may not be correct (and therefore functions like
    /// <see cref="CalculateMemPoolAncestors(TxMempoolEntry, TxMempool.SetEntries, long, long, long, long, out string, bool)"/>
    /// and <see cref="CalculateDescendants(TxMempoolEntry, TxMempool.SetEntries)"/> that rely
    /// on them to walk the mempool are not generally safe to use).
    ///
    /// Computational limits:
    ///
    /// Updating all in-mempool ancestors of a newly added transaction can be slow,
    /// if no bound exists on how many in-mempool ancestors there may be.
    /// <see cref="CalculateMemPoolAncestors(TxMempoolEntry, TxMempool.SetEntries, long, long, long, long, out string, bool)"/>
    /// takes configurable limits that are designed to
    /// prevent these calculations from being too CPU intensive.
    ///
    /// Adding transactions from a disconnected block can be very time consuming,
    /// because we don't have a way to limit the number of in-mempool descendants.
    /// To bound CPU processing, we limit the amount of work we're willing to do
    /// to properly update the descendant information for a tx being added from
    /// a disconnected block.  If we would exceed the limit, then we instead mark
    /// the entry as "dirty", and set the feerate for sorting purposes to be equal
    /// the feerate of the transaction without any descendants.
    /// </remarks>
    public class TokenlessMempool : ITxMempool
    {
        /// <summary> The indexed transaction set in the memory pool.</summary>
        public IndexedTransactionSet MapTx { get; }

        /// <summary> Collection of transaction inputs.</summary>
        public List<NextTxPair> MapNextTx { get; }

        /// <summary> Value n means that n times in 2^32 we check.</summary>
        private double checkFrequency;

        /// <summary> Sum of dynamic memory usage of all the map elements (NOT the maps themselves).</summary>
        private long cachedInnerUsage;

        /// <summary> Gets the miner policy estimator.</summary>
        public BlockPolicyEstimator MinerPolicyEstimator { get; }

        /// <summary>
        /// minReasonableRelayFee should be a feerate which is, roughly, somewhere
        /// around what it "costs" to relay a transaction around the network and
        /// below which we would reasonably say a transaction has 0-effective-fee.
        ///  </summary>
        private readonly FeeRate minReasonableRelayFee;

        /// <summary> Minimum fee to get into the pool, decreases exponentially.</summary>
        private double rollingMinimumFeeRate;

        /// <summary> Collection of transaction links.</summary>
        private readonly TxlinksMap mapLinks;

        /// <summary> Dictionary of <see cref="DeltaPair"/> indexed by transaction hash.</summary>
        private readonly Dictionary<uint256, DeltaPair> mapDeltas;

        private readonly ILogger logger;
        private readonly ISignals signals;

        public TokenlessMempool(BlockPolicyEstimator blockPolicyEstimator, ILoggerFactory loggerFactory, NodeSettings nodeSettings, ISignals signals = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.signals = signals;

            this.MapTx = new IndexedTransactionSet();
            this.mapLinks = new TxlinksMap();
            this.MapNextTx = new List<NextTxPair>();
            this.mapDeltas = new Dictionary<uint256, DeltaPair>();

            this.InnerClear(); //lock free clear

            // Sanity checks off by default for performance, because otherwise
            // accepting transactions becomes O(N^2) where N is the number
            // of transactions in the pool
            this.checkFrequency = 0;

            this.MinerPolicyEstimator = blockPolicyEstimator;
            this.minReasonableRelayFee = nodeSettings.MinRelayTxFeeRate;
        }

        /// <summary>Get the number of transactions in the memory pool.</summary>
        public long Size
        {
            get { return this.MapTx.Count; }
        }

        /// <summary>
        /// Clears the collections that contain the memory pool transactions,
        /// and increments the running total of transactions updated.
        /// </summary>
        private void InnerClear()
        {
            this.mapLinks.Clear();
            this.MapTx.Clear();
            this.MapNextTx.Clear();
            this.cachedInnerUsage = 0;
            this.rollingMinimumFeeRate = 0;
        }

        /// <inheritdoc />
        public void Clear()
        {
            this.InnerClear();
        }

        /// <inheritdoc />
        public void Check(ICoinView pcoins)
        {
            if (this.checkFrequency == 0)
                return;

            if (new Random(int.MaxValue).Next() >= this.checkFrequency)
                return;

            this.logger.LogInformation($"Checking mempool with {this.MapTx.Count} transactions and {this.MapNextTx.Count} inputs");

            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Transaction Get(uint256 hash)
        {
            return this.MapTx.TryGet(hash)?.Transaction;
        }

        /// <inheritdoc />
        public FeeRate EstimateFee(int nBlocks)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public FeeRate EstimateSmartFee(int nBlocks, out int answerFoundAtBlocks)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public double EstimateSmartPriority(int nBlocks, out int answerFoundAtBlocks)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void SetSanityCheck(double dFrequency = 1.0)
        {
            this.checkFrequency = dFrequency * 4294967295.0;
        }

        /// <inheritdoc />
        public bool AddUnchecked(uint256 hash, TxMempoolEntry entry, bool validFeeEstimate = true)
        {
            var setAncestors = new SetEntries();
            this.CalculateMemPoolAncestors(entry, setAncestors, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, out _);
            return this.AddUnchecked(hash, entry, setAncestors, validFeeEstimate);
        }

        /// <inheritdoc />
        public bool AddUnchecked(uint256 hash, TxMempoolEntry mempoolEntry, SetEntries setAncestors, bool validFeeEstimate = true)
        {
            this.MapTx.Add(mempoolEntry);
            this.mapLinks.Add(mempoolEntry, new TxLinks { Parents = new SetEntries(), Children = new SetEntries() });

            // Update cachedInnerUsage to include contained transaction's usage.
            // (When we update the entry for in-mempool parents, memory usage will be
            // further updated.)
            this.cachedInnerUsage += mempoolEntry.DynamicMemoryUsage();

            var setParentTransactions = new HashSet<uint256>();
            foreach (TxIn txInput in mempoolEntry.Transaction.Inputs)
            {
                this.MapNextTx.Add(new NextTxPair { OutPoint = txInput.PrevOut, Transaction = mempoolEntry.Transaction });
                setParentTransactions.Add(txInput.PrevOut.Hash);
            }

            // Don't bother worrying about child transactions of this one.
            // Normal case of a new transaction arriving is that there can't be any
            // children, because such children would be orphans.
            // An exception to that is if a transaction enters that used to be in a block.
            // In that case, our disconnect block logic will call UpdateTransactionsFromBlock
            // to clean up the mess we're leaving here.

            // Update ancestors with information about this tx
            foreach (uint256 parentHash in setParentTransactions)
            {
                TxMempoolEntry parentMempoolEntry = this.MapTx.TryGet(parentHash);
                if (parentMempoolEntry != null)
                    this.UpdateParent(mempoolEntry, parentMempoolEntry, true);
            }

            this.UpdateAncestorsOf(true, mempoolEntry, setAncestors);
            this.UpdateEntryForAncestors(mempoolEntry, setAncestors);

            if (this.signals != null)
                this.signals.Publish(new TransactionAddedToMemoryPool(mempoolEntry.Transaction));

            return true;
        }

        /// <summary>
        /// Set ancestor state for an entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="mempoolEntries">Transaction ancestors.</param>
        private void UpdateEntryForAncestors(TxMempoolEntry entry, SetEntries mempoolEntries)
        {
            long updateSize = 0;

            foreach (TxMempoolEntry ancestorMempoolEntry in mempoolEntries)
            {
                updateSize += ancestorMempoolEntry.GetTxSize();
            }

            entry.UpdateAncestorState(updateSize, 0, mempoolEntries.Count, 0);
        }

        /// <summary>
        /// Update ancestors of hash to add/remove it as a descendant transaction.
        /// </summary>
        /// <param name="add">Whether to add or remove.</param>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="entries">Transaction ancestors.</param>
        private void UpdateAncestorsOf(bool add, TxMempoolEntry entry, SetEntries entries)
        {
            SetEntries parentEntries = this.GetMemPoolParents(entry);

            // Add or remove this transaction as a child of each parent.
            foreach (TxMempoolEntry parentEntry in parentEntries)
                this.UpdateChild(parentEntry, entry, add);

            long updateCount = (add ? 1 : -1);
            long updateSize = updateCount * entry.GetTxSize();

            foreach (TxMempoolEntry ancestorIt in entries)
            {
                ancestorIt.UpdateDescendantState(updateSize, 0, updateCount);
            }
        }

        /// <summary>
        /// Gets the parents of a memory pool entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <returns>Set of parent entries.</returns>
        private SetEntries GetMemPoolParents(TxMempoolEntry entry)
        {
            Guard.NotNull(entry, nameof(entry));

            Guard.Assert(this.MapTx.ContainsKey(entry.TransactionHash));
            TxLinks it = this.mapLinks.TryGet(entry);
            Guard.Assert(it != null);

            return it.Parents;
        }

        /// <summary>
        /// Gets the children of a memory pool entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <returns>Set of child entries.</returns>
        private SetEntries GetMemPoolChildren(TxMempoolEntry entry)
        {
            Guard.NotNull(entry, nameof(entry));

            Guard.Assert(this.MapTx.ContainsKey(entry.TransactionHash));
            TxLinks it = this.mapLinks.TryGet(entry);
            Guard.Assert(it != null);

            return it.Children;
        }

        /// <summary>
        /// Updates memory pool entry with a child.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="child">Child entry to add/remove.</param>
        /// <param name="add">Whether to add or remove entry.</param>
        private void UpdateChild(TxMempoolEntry entry, TxMempoolEntry child, bool add)
        {
            // todo: find how to take a memory size of SetEntries
            //setEntries s;
            if (add && this.mapLinks[entry].Children.Add(child))
            {
                this.cachedInnerUsage += child.DynamicMemoryUsage();
            }
            else if (!add && this.mapLinks[entry].Children.Remove(child))
            {
                this.cachedInnerUsage -= child.DynamicMemoryUsage();
            }
        }

        /// <summary>
        /// Updates memory pool entry with a parent.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="parent">Parent entry to add/remove.</param>
        /// <param name="add">Whether to add or remove entry.</param>
        private void UpdateParent(TxMempoolEntry entry, TxMempoolEntry parent, bool add)
        {
            // todo: find how to take a memory size of SetEntries
            //SetEntries s;
            if (add && this.mapLinks[entry].Parents.Add(parent))
            {
                this.cachedInnerUsage += parent.DynamicMemoryUsage();
            }
            else if (!add && this.mapLinks[entry].Parents.Remove(parent))
            {
                this.cachedInnerUsage -= parent.DynamicMemoryUsage();
            }
        }

        /// <inheritdoc />
        public bool CalculateMemPoolAncestors(
            TxMempoolEntry entry,
            SetEntries ancestorEntries,
            long limitAncestorCount,
            long limitAncestorSize,
            long limitDescendantCount,
            long limitDescendantSize,
            out string errString,
            bool fSearchForParents = true)
        {
            errString = string.Empty;

            var parentHashes = new SetEntries();

            if (fSearchForParents)
            {
                // Get parents of this transaction that are in the mempool
                // GetMemPoolParents() is only valid for entries in the mempool, so we
                // iterate mapTx to find parents.
                foreach (TxIn txInput in entry.Transaction.Inputs)
                {
                    TxMempoolEntry piter = this.MapTx.TryGet(txInput.PrevOut.Hash);
                    if (piter != null)
                    {
                        parentHashes.Add(piter);
                        if (parentHashes.Count + 1 > limitAncestorCount)
                        {
                            errString = $"too many unconfirmed parents [limit: {limitAncestorCount}]";
                            this.logger.LogTrace("(-)[TOO_MANY_UNCONFIRM_PARENTS]:false");
                            return false;
                        }
                    }
                }
            }
            else
            {
                if (this.MapTx.ContainsKey(entry.TransactionHash))
                {
                    // If we're not searching for parents, we require this to be an
                    // entry in the mempool already.
                    //var it = mapTx.Txids.TryGet(entry.TransactionHash);
                    SetEntries memPoolParents = this.GetMemPoolParents(entry);
                    foreach (TxMempoolEntry item in memPoolParents)
                        parentHashes.Add(item);
                }
            }

            long totalSizeWithAncestors = entry.GetTxSize();

            while (parentHashes.Any())
            {
                TxMempoolEntry stageit = parentHashes.First();

                ancestorEntries.Add(stageit);
                parentHashes.Remove(stageit);
                totalSizeWithAncestors += stageit.GetTxSize();

                if (stageit.SizeWithDescendants + entry.GetTxSize() > limitDescendantSize)
                {
                    errString = $"exceeds descendant size limit for tx {stageit.TransactionHash} [limit: {limitDescendantSize}]";
                    this.logger.LogTrace("(-)[EXCEED_DECENDANT_SIZE_LIMIT]:false");
                    return false;
                }
                else if (stageit.CountWithDescendants + 1 > limitDescendantCount)
                {
                    errString = $"too many descendants for tx {stageit.TransactionHash} [limit: {limitDescendantCount}]";
                    this.logger.LogTrace("(-)[TOO_MANY_DECENDANTS]:false");
                    return false;
                }
                else if (totalSizeWithAncestors > limitAncestorSize)
                {
                    errString = $"exceeds ancestor size limit [limit: {limitAncestorSize}]";
                    this.logger.LogTrace("(-)[EXCEED_ANCESTOR_SIZE_LIMIT]:false");
                    return false;
                }

                SetEntries setMemPoolParents = this.GetMemPoolParents(stageit);
                foreach (TxMempoolEntry phash in setMemPoolParents)
                {
                    // If this is a new ancestor, add it.
                    if (!ancestorEntries.Contains(phash))
                    {
                        parentHashes.Add(phash);
                    }

                    if (parentHashes.Count + ancestorEntries.Count + 1 > limitAncestorCount)
                    {
                        errString = $"too many unconfirmed ancestors [limit: {limitAncestorCount}]";
                        this.logger.LogTrace("(-)[TOO_MANY_UNCONFIRM_ANCESTORS]:false");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <inheritdoc />
        public bool HasNoInputsOf(Transaction tx)
        {
            foreach (TxIn txInput in tx.Inputs)
            {
                if (this.Exists(txInput.PrevOut.Hash))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public bool Exists(uint256 hash)
        {
            return this.MapTx.ContainsKey(hash);
        }

        /// <inheritdoc />
        public void RemoveRecursive(Transaction originalTx)
        {
            // Remove transaction from memory pool
            uint256 originalTransactionHash = originalTx.GetHash();

            var transactionsToRemove = new SetEntries();

            TxMempoolEntry originalEntry = this.MapTx.TryGet(originalTransactionHash);
            if (originalEntry != null)
                transactionsToRemove.Add(originalEntry);
            else
            {
                // When recursively removing but origTx isn't in the mempool
                // be sure to remove any children that are in the pool. This can
                // happen during chain re-orgs if origTx isn't re-accepted into
                // the mempool for any reason.
                for (int i = 0; i < originalTx.Outputs.Count; i++)
                {
                    NextTxPair it = this.MapNextTx.FirstOrDefault(w => w.OutPoint == new OutPoint(originalTransactionHash, i));
                    if (it == null)
                        continue;
                    TxMempoolEntry nextit = this.MapTx.TryGet(it.Transaction.GetHash());
                    Guard.Assert(nextit != null);
                    transactionsToRemove.Add(nextit);
                }
            }

            var setAllRemoves = new SetEntries();

            foreach (TxMempoolEntry item in transactionsToRemove)
            {
                this.CalculateDescendants(item, setAllRemoves);
            }

            this.RemoveStaged(setAllRemoves, false);
        }

        /// <inheritdoc />
        public void RemoveStaged(SetEntries stage, bool updateDescendants)
        {
            this.UpdateForRemoveFromMempool(stage, updateDescendants);

            foreach (TxMempoolEntry it in stage)
            {
                this.RemoveUnchecked(it);
            }
        }

        /// <inheritdoc />
        public int Expire(long time)
        {
            var toremove = new SetEntries();
            foreach (TxMempoolEntry entry in this.MapTx.EntryTime)
            {
                if (!(entry.Time < time)) break;
                toremove.Add(entry);
            }

            var stage = new SetEntries();
            foreach (TxMempoolEntry removeit in toremove)
            {
                this.CalculateDescendants(removeit, stage);
            }

            this.RemoveStaged(stage, false);

            return stage.Count;
        }

        /// <summary>
        /// Removes entry from memory pool.
        /// </summary>
        /// <param name="entry">Entry to remove.</param>
        private void RemoveUnchecked(TxMempoolEntry entry)
        {
            uint256 hash = entry.TransactionHash;
            foreach (TxIn txin in entry.Transaction.Inputs)
            {
                this.MapNextTx.Remove(this.MapNextTx.FirstOrDefault(w => w.OutPoint == txin.PrevOut));
            }

            this.cachedInnerUsage -= entry.DynamicMemoryUsage();
            this.cachedInnerUsage -= this.mapLinks[entry]?.Parents?.Sum(p => p.DynamicMemoryUsage()) ?? 0 + this.mapLinks[entry]?.Children?.Sum(p => p.DynamicMemoryUsage()) ?? 0;
            this.mapLinks.Remove(entry);
            this.MapTx.Remove(entry);
            this.MinerPolicyEstimator.RemoveTx(hash);

            if (this.signals != null)
            {
                this.signals.Publish(new TransactionRemovedFromMemoryPool(entry.Transaction));
            }
        }

        /// <inheritdoc />
        public void CalculateDescendants(TxMempoolEntry entry, SetEntries setDescendants)
        {
            var stage = new SetEntries();
            if (!setDescendants.Contains(entry))
            {
                stage.Add(entry);
            }

            // Traverse down the children of entry, only adding children that are not
            // accounted for in setDescendants already (because those children have either
            // already been walked, or will be walked in this iteration).
            while (stage.Any())
            {
                TxMempoolEntry it = stage.First();
                setDescendants.Add(it);
                stage.Remove(it);

                SetEntries setChildren = this.GetMemPoolChildren(it);
                foreach (TxMempoolEntry childiter in setChildren)
                {
                    if (!setDescendants.Contains(childiter))
                    {
                        stage.Add(childiter);
                    }
                }
            }
        }

        /// <summary>
        /// For each transaction being removed, update ancestors and any direct children.
        /// </summary>
        /// <param name="entriesToRemove">Memory pool entries to remove.</param>
        /// <param name="updateDescendants">If updateDescendants is true, then also update in-mempool descendants' ancestor state.</param>
        private void UpdateForRemoveFromMempool(SetEntries entriesToRemove, bool updateDescendants)
        {
            // For each entry, walk back all ancestors and decrement size associated with this transaction.

            if (updateDescendants)
            {
                // updateDescendants should be true whenever we're not recursively
                // removing a tx and all its descendants, eg when a transaction is
                // confirmed in a block.
                // Here we only update statistics and not data in mapLinks (which
                // we need to preserve until we're finished with all operations that
                // need to traverse the mempool).
                foreach (TxMempoolEntry removeIt in entriesToRemove)
                {
                    var setDescendants = new SetEntries();
                    this.CalculateDescendants(removeIt, setDescendants);
                    setDescendants.Remove(removeIt); // don't update state for self
                    long modifySize = -removeIt.GetTxSize();

                    foreach (TxMempoolEntry dit in setDescendants)
                        dit.UpdateAncestorState(modifySize, 0, -1, 0);
                }
            }

            foreach (TxMempoolEntry entry in entriesToRemove)
            {
                var setAncestors = new SetEntries();

                // Since this is a tx that is already in the mempool, we can call CMPA
                // with fSearchForParents = false.  If the mempool is in a consistent
                // state, then using true or false should both be correct, though false
                // should be a bit faster.
                // However, if we happen to be in the middle of processing a reorg, then
                // the mempool can be in an inconsistent state.  In this case, the set
                // of ancestors reachable via mapLinks will be the same as the set of
                // ancestors whose packages include this transaction, because when we
                // add a new transaction to the mempool in addUnchecked(), we assume it
                // has no children, and in the case of a reorg where that assumption is
                // false, the in-mempool children aren't linked to the in-block tx's
                // until UpdateTransactionsFromBlock() is called.
                // So if we're being called during a reorg, ie before
                // UpdateTransactionsFromBlock() has been called, then mapLinks[] will
                // differ from the set of mempool parents we'd calculate by searching,
                // and it's important that we use the mapLinks[] notion of ancestor
                // transactions as the set of things to update for removal.
                this.CalculateMemPoolAncestors(entry, setAncestors, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, out _, false);

                // Note that UpdateAncestorsOf severs the child links that point to
                // removeIt in the entries for the parents of removeIt.
                this.UpdateAncestorsOf(false, entry, setAncestors);
            }

            // After updating all the ancestor sizes, we can now sever the link between each
            // transaction being removed and any mempool children (ie, update setMemPoolParents
            // for each direct child of a transaction being removed).
            foreach (TxMempoolEntry removeIt in entriesToRemove)
            {
                this.UpdateChildrenForRemoval(removeIt);
            }
        }

        /// <summary>
        /// Sever link between specified transaction and direct children.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        private void UpdateChildrenForRemoval(TxMempoolEntry entry)
        {
            SetEntries setMemPoolChildren = this.GetMemPoolChildren(entry);

            foreach (TxMempoolEntry updateIt in setMemPoolChildren)
                this.UpdateParent(updateIt, entry, false);
        }

        /// <inheritdoc />
        public void RemoveForBlock(IEnumerable<Transaction> transactions, int blockHeight)
        {
            var entries = new List<TxMempoolEntry>();

            foreach (Transaction transaction in transactions)
            {
                uint256 hash = transaction.GetHash();
                TxMempoolEntry entry = this.MapTx.TryGet(hash);
                if (entry != null)
                    entries.Add(entry);
            }

            // Before the txs in the new block have been removed from the mempool, update policy estimates
            this.MinerPolicyEstimator.ProcessBlock(blockHeight, entries);
            foreach (Transaction transaction in transactions)
            {
                uint256 hash = transaction.GetHash();

                TxMempoolEntry entry = this.MapTx.TryGet(hash);
                if (entry != null)
                {
                    var stage = new SetEntries
                    {
                        entry
                    };

                    this.RemoveStaged(stage, true);
                }

                this.RemoveConflicts(transaction);
                this.ClearPrioritisation(transaction.GetHash());
            }
        }

        /// <summary> Removes conflicting transactions.</summary>
        /// <param name="transaction">Transaction to remove conflicts from.</param>
        private void RemoveConflicts(Transaction transaction)
        {
            // Remove transactions which depend on inputs of tx, recursively
            foreach (TxIn txInput in transaction.Inputs)
            {
                NextTxPair it = this.MapNextTx.FirstOrDefault(p => p.OutPoint == txInput.PrevOut);
                if (it != null)
                {
                    Transaction txConflict = it.Transaction;
                    if (txConflict != transaction)
                    {
                        this.ClearPrioritisation(txConflict.GetHash());
                        this.RemoveRecursive(txConflict);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the prioritisation for a transaction.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        private void ClearPrioritisation(uint256 hash)
        {
            this.mapDeltas.Remove(hash);
        }

        /// <summary>
        /// Get the amount of dynamic memory being used by the memory pool.
        /// </summary>
        /// <returns>Number of bytes in use by memory pool.</returns>
        public long DynamicMemoryUsage()
        {
            return this.MapTx.Values.Sum(m => m.DynamicMemoryUsage()) + this.cachedInnerUsage;
        }

        /// <inheritdoc />
        public void TrimToSize(long sizelimit, List<uint256> pvNoSpendsRemaining = null)
        {
            while (this.MapTx.Any() && this.DynamicMemoryUsage() > sizelimit)
            {
                TxMempoolEntry entry = this.MapTx.DescendantScore.First();

                var stage = new SetEntries();
                this.CalculateDescendants(entry, stage);

                var transactions = new List<Transaction>();
                if (pvNoSpendsRemaining != null)
                {
                    foreach (TxMempoolEntry setEntry in stage)
                        transactions.Add(setEntry.Transaction);
                }

                this.RemoveStaged(stage, false);

                if (pvNoSpendsRemaining == null)
                {
                    foreach (Transaction tx in transactions)
                    {
                        foreach (TxIn txin in tx.Inputs)
                        {
                            if (this.Exists(txin.PrevOut.Hash))
                                continue;
                            NextTxPair iter = this.MapNextTx.FirstOrDefault(p => p.OutPoint == new OutPoint(txin.PrevOut.Hash, 0));
                            if (iter == null || iter.OutPoint.Hash != txin.PrevOut.Hash)
                                pvNoSpendsRemaining.Add(txin.PrevOut.Hash);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public FeeRate GetMinFee(long sizelimit)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ApplyDeltas(uint256 hash, ref double dPriorityDelta, ref Money nFeeDelta)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void WriteFeeEstimates(BitcoinStream stream)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void ReadFeeEstimates(BitcoinStream stream)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }
    }
}
