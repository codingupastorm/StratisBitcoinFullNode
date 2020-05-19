using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Fee;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Core.Signals;
using static Stratis.Features.MemoryPool.TxMempool;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    /// <inheritdoc />
    public sealed class TokenlessMempool : ITxMempool
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

        /// <summary> Collection of transaction links.</summary>
        private readonly TxlinksMap mapLinks;

        private readonly ILogger logger;

        private readonly ISignals signals;

        public TokenlessMempool(BlockPolicyEstimator blockPolicyEstimator, ILoggerFactory loggerFactory, ISignals signals = null)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.signals = signals;

            this.MapTx = new IndexedTransactionSet();
            this.mapLinks = new TxlinksMap();

            this.InnerClear();

            // Sanity checks off by default for performance, because otherwise
            // accepting transactions becomes O(N^2) where N is the number
            // of transactions in the pool
            this.checkFrequency = 0;

            this.MinerPolicyEstimator = blockPolicyEstimator;
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
            this.cachedInnerUsage = 0;
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
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public FeeRate EstimateSmartFee(int nBlocks, out int answerFoundAtBlocks)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public double EstimateSmartPriority(int nBlocks, out int answerFoundAtBlocks)
        {
            // Not valid in a tokenless blockchain.
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
            return this.AddUnchecked(hash, entry, null, validFeeEstimate);
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

            if (this.signals != null)
                this.signals.Publish(new TransactionAddedToMemoryPool(mempoolEntry.Transaction));

            return true;
        }

        /// <inheritdoc />
        public bool CalculateMemPoolAncestors(TxMempoolEntry entry, SetEntries ancestorEntries, long limitAncestorCount, long limitAncestorSize, long limitDescendantCount, long limitDescendantSize, out string errString, bool fSearchForParents = true)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool HasNoInputsOf(Transaction tx)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public bool Exists(uint256 transactionHash)
        {
            return this.MapTx.ContainsKey(transactionHash);
        }

        /// <inheritdoc />
        public void RemoveRecursive(Transaction originalTx)
        {
            // Remove transaction from memory pool
            uint256 originalTransactionHash = originalTx.GetHash();

            var entriesToRemove = new SetEntries();

            TxMempoolEntry originalEntry = this.MapTx.TryGet(originalTransactionHash);
            if (originalEntry != null)
                entriesToRemove.Add(originalEntry);

            this.RemoveStaged(entriesToRemove, false);
        }

        /// <inheritdoc />
        public void RemoveStaged(SetEntries entriesToRemove, bool updateDescendants)
        {
            foreach (TxMempoolEntry entry in entriesToRemove)
            {
                this.RemoveUnchecked(entry);
            }
        }

        /// <inheritdoc />
        public int Expire(long time)
        {
            var entriesToRemove = new SetEntries();
            foreach (TxMempoolEntry entry in this.MapTx.EntryTime)
            {
                if (!(entry.Time < time))
                    break;

                entriesToRemove.Add(entry);
            }

            this.RemoveStaged(entriesToRemove, false);

            return entriesToRemove.Count;
        }

        /// <summary>
        /// Removes entry from memory pool.
        /// </summary>
        /// <param name="mempoolEntry">Entry to remove.</param>
        private void RemoveUnchecked(TxMempoolEntry mempoolEntry)
        {
            this.cachedInnerUsage -= mempoolEntry.DynamicMemoryUsage();
            this.cachedInnerUsage -= this.mapLinks[mempoolEntry]?.Parents?.Sum(p => p.DynamicMemoryUsage()) ?? 0 + this.mapLinks[mempoolEntry]?.Children?.Sum(p => p.DynamicMemoryUsage()) ?? 0;
            this.mapLinks.Remove(mempoolEntry);
            this.MapTx.Remove(mempoolEntry);
            this.MinerPolicyEstimator.RemoveTx(mempoolEntry.TransactionHash);

            if (this.signals != null)
                this.signals.Publish(new TransactionRemovedFromMemoryPool(mempoolEntry.Transaction));
        }

        /// <inheritdoc />
        public void CalculateDescendants(TxMempoolEntry entry, SetEntries setDescendants)
        {
            // Not valid in a tokenless blockchain.
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void RemoveForBlock(IEnumerable<Transaction> transactions, int blockHeight)
        {
            var entries = new List<TxMempoolEntry>();

            foreach (Transaction transaction in transactions)
            {
                TxMempoolEntry mempoolEntry = this.MapTx.TryGet(transaction.GetHash());
                if (mempoolEntry != null)
                    entries.Add(mempoolEntry);
            }

            // Before the txs in the new block have been removed from the mempool, update policy estimates
            this.MinerPolicyEstimator.ProcessBlock(blockHeight, entries);

            foreach (Transaction transaction in transactions)
            {
                TxMempoolEntry mempoolEntry = this.MapTx.TryGet(transaction.GetHash());
                if (mempoolEntry != null)
                {
                    var stage = new SetEntries
                    {
                        mempoolEntry
                    };

                    this.RemoveStaged(stage, true);
                }
            }
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

                var entriesToRemove = new SetEntries { entry };

                var transactions = new List<Transaction>();

                if (pvNoSpendsRemaining != null)
                {
                    foreach (TxMempoolEntry setEntry in entriesToRemove)
                        transactions.Add(setEntry.Transaction);
                }

                this.RemoveStaged(entriesToRemove, false);

                if (pvNoSpendsRemaining == null)
                {
                    foreach (Transaction tx in transactions)
                    {
                        foreach (TxIn txin in tx.Inputs)
                        {
                            if (this.Exists(txin.PrevOut.Hash))
                                continue;
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
