using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mining
{
    public sealed class TokenlessBlockDefinition : BlockDefinition
    {
        private readonly ICoinView coinView;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ILogger logger;
        private readonly List<Receipt> receipts;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly ICallDataSerializer callDataSerializer;
        private IStateRepositoryRoot stateSnapshot;
        private readonly ISenderRetriever senderRetriever;

        public TokenlessBlockDefinition(
            IBlockBufferGenerator blockBufferGenerator,
            ICoinView coinView,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            MinerSettings minerSettings,
            Network network,
            ISenderRetriever senderRetriever,
            IStateRepositoryRoot stateRoot,
            IBlockExecutionResultCache executionCache,
            ICallDataSerializer callDataSerializer)
            : base(consensusManager, dateTimeProvider, loggerFactory, mempool, mempoolLock, minerSettings, network)
        {
            this.coinView = coinView;
            this.executorFactory = executorFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.senderRetriever = senderRetriever;
            this.stateRoot = stateRoot;
            this.callDataSerializer = callDataSerializer;
            this.executionCache = executionCache;
            this.receipts = new List<Receipt>();

            // When building smart contract blocks, we will be generating and adding both transactions to the block and txouts to the coinbase. 
            // At the moment, these generated objects aren't accounted for in the block size and weight accounting. 
            // This means that if blocks started getting full, this miner could start generating blocks greater than the max consensus block size.
            // To avoid this without significantly overhauling the BlockDefinition, for now we just lower the block size by a percentage buffer.
            // If in the future blocks are being built over the size limit and you need an easy fix, just increase the size of this buffer.
            this.Options = blockBufferGenerator.GetOptionsWithBuffer(this.Options);
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            this.ChainTip = chainTip;

            // TODO-TL: Implement new way to get sender
            GetSenderResult getSenderResult = this.senderRetriever.GetAddressFromScript(scriptPubKey);

            this.stateSnapshot = this.stateRoot.GetSnapshotTo(((ISmartContractBlockHeader)this.ConsensusManager.Tip.Header).HashStateRoot.ToBytes());

            this.receipts.Clear();

            base.Configure();

            this.Block = this.BlockTemplate.Block;
            this.scriptPubKey = scriptPubKey;

            this.ComputeBlockVersion();

            this.MedianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.LockTimeCutoff = MempoolValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast)
                ? this.MedianTimePast
                : this.Block.Header.Time;

            // Add transactions from the mempool
            this.AddTransactions(out _, out _);

            this.logger.LogDebug("Serialized size is {0} bytes, block weight is {1}, number of txs is {2}, tx fees are {3}, number of sigops is {4}.", this.Block.GetSerializedSize(), this.Block.GetBlockWeight(this.Network.Consensus), this.BlockTx, this.fees, this.BlockSigOpsCost);

            this.UpdateHeaders();

            // Cache the results. We don't need to execute these again when validating.
            var cacheModel = new BlockExecutionResultModel(this.stateSnapshot, this.receipts);
            this.executionCache.StoreExecutionResult(this.BlockTemplate.Block.GetHash(), cacheModel);

            return this.BlockTemplate;
        }

        protected virtual void AddTransactions(out int nPackagesSelected, out int nDescendantsUpdated)
        {
            nPackagesSelected = 0;
            nDescendantsUpdated = 0;

            // mapModifiedTx will store sorted packages after they are modified
            // because some of their txs are already in the block.
            var mapModifiedTx = new Dictionary<uint256, TxMemPoolModifiedEntry>();

            //var mapModifiedTxRes = this.mempoolScheduler.ReadAsync(() => mempool.MapTx.Values).GetAwaiter().GetResult();
            // mapModifiedTxRes.Select(s => new TxMemPoolModifiedEntry(s)).OrderBy(o => o, new CompareModifiedEntry());

            // Keep track of entries that failed inclusion, to avoid duplicate work.
            var failedTx = new TxMempool.SetEntries();

            // Start by adding all descendants of previously added txs to mapModifiedTx
            // and modifying them for their already included ancestors.
            this.UpdatePackagesForAdded(this.inBlock, mapModifiedTx);

            // TODO-TL: Order by time stamp here
            var entriesOrderByTime = this.MempoolLock.ReadAsync(() => this.Mempool.MapTx.AncestorScore).ConfigureAwait(false).GetAwaiter().GetResult().ToList();

            TxMempoolEntry mempoolEntry;

            int nConsecutiveFailed = 0;
            while (entriesOrderByTime.Any() || mapModifiedTx.Any())
            {
                TxMempoolEntry mi = entriesOrderByTime.FirstOrDefault();
                if (mi != null)
                {
                    // Skip entries in mapTx that are already in a block or are present
                    // in mapModifiedTx (which implies that the mapTx ancestor state is
                    // stale due to ancestor inclusion in the block).
                    // Also skip transactions that we've already failed to add. This can happen if
                    // we consider a transaction in mapModifiedTx and it fails: we can then
                    // potentially consider it again while walking mapTx.  It's currently
                    // guaranteed to fail again, but as a belt-and-suspenders check we put it in
                    // failedTx and avoid re-evaluation, since the re-evaluation would be using
                    // cached size/sigops/fee values that are not actually correct.

                    // First try to find a new transaction in mapTx to evaluate.
                    if (mapModifiedTx.ContainsKey(mi.TransactionHash) || this.inBlock.Contains(mi) || failedTx.Contains(mi))
                    {
                        entriesOrderByTime.Remove(mi);
                        continue;
                    }
                }

                // Now that mi is not stale, determine which transaction to evaluate:
                // the next entry from mapTx, or the best from mapModifiedTx?
                bool fUsingModified = false;
                TxMemPoolModifiedEntry modit;
                var compare = new CompareModifiedEntry();
                if (mi == null)
                {
                    modit = mapModifiedTx.Values.OrderBy(o => o, compare).First();
                    mempoolEntry = modit.MempoolEntry;
                    fUsingModified = true;
                }
                else
                {
                    // Try to compare the mapTx entry to the mapModifiedTx entry
                    mempoolEntry = mi;

                    modit = mapModifiedTx.Values.OrderBy(o => o, compare).FirstOrDefault();
                    if ((modit != null) && (compare.Compare(modit, new TxMemPoolModifiedEntry(mempoolEntry)) < 0))
                    {
                        // The best entry in mapModifiedTx has higher score
                        // than the one from mapTx..
                        // Switch which transaction (package) to consider.

                        mempoolEntry = modit.MempoolEntry;
                        fUsingModified = true;
                    }
                    else
                    {
                        // Either no entry in mapModifiedTx, or it's worse than mapTx.
                        // Increment mi for the next loop iteration.
                        entriesOrderByTime.Remove(mempoolEntry);
                    }
                }

                // We skip mapTx entries that are inBlock, and mapModifiedTx shouldn't
                // contain anything that is inBlock.
                Guard.Assert(!this.inBlock.Contains(mempoolEntry));

                long packageSize = mempoolEntry.SizeWithAncestors;
                Money packageFees = mempoolEntry.ModFeesWithAncestors;
                long packageSigOpsCost = mempoolEntry.SigOpCostWithAncestors;
                if (fUsingModified)
                {
                    packageSize = modit.SizeWithAncestors;
                    packageFees = modit.ModFeesWithAncestors;
                    packageSigOpsCost = modit.SigOpCostWithAncestors;
                }

                if (packageFees < this.BlockMinFeeRate.GetFee((int)packageSize))
                {
                    // Everything else we might consider has a lower fee rate
                    return;
                }

                if (!this.TestPackage(mempoolEntry, packageSize, packageSigOpsCost))
                {
                    if (fUsingModified)
                    {
                        // Since we always look at the best entry in mapModifiedTx,
                        // we must erase failed entries so that we can consider the
                        // next best entry on the next loop iteration
                        mapModifiedTx.Remove(modit.MempoolEntry.TransactionHash);
                        failedTx.Add(mempoolEntry);
                    }

                    nConsecutiveFailed++;

                    if ((nConsecutiveFailed > MaxConsecutiveAddTransactionFailures) && (this.BlockWeight > this.Options.BlockMaxWeight - 4000))
                    {
                        // Give up if we're close to full and haven't succeeded in a while
                        break;
                    }
                    continue;
                }

                var ancestors = new TxMempool.SetEntries();
                long nNoLimit = long.MaxValue;
                string dummy;

                this.MempoolLock.ReadAsync(() => this.Mempool.CalculateMemPoolAncestors(mempoolEntry, ancestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false)).ConfigureAwait(false).GetAwaiter().GetResult();

                this.OnlyUnconfirmed(ancestors);
                ancestors.Add(mempoolEntry);

                // Test if all tx's are Final.
                if (!this.TestPackageTransactions(ancestors))
                {
                    if (fUsingModified)
                    {
                        mapModifiedTx.Remove(modit.MempoolEntry.TransactionHash);
                        failedTx.Add(mempoolEntry);
                    }
                    continue;
                }

                // This transaction will make it in; reset the failed counter.
                nConsecutiveFailed = 0;

                // Package can be added. Sort the entries in a valid order.
                // Sort package by ancestor count
                // If a transaction A depends on transaction B, then A's ancestor count
                // must be greater than B's.  So this is sufficient to validly order the
                // transactions for block inclusion.
                List<TxMempoolEntry> sortedEntries = ancestors.ToList().OrderBy(o => o, new CompareTxIterByAncestorCount()).ToList();
                foreach (TxMempoolEntry sortedEntry in sortedEntries)
                {
                    this.AddToBlock(sortedEntry);
                    // Erase from the modified set, if present
                    mapModifiedTx.Remove(sortedEntry.TransactionHash);
                }

                nPackagesSelected++;

                // Update transactions that depend on each of these
                nDescendantsUpdated += this.UpdatePackagesForAdded(ancestors, mapModifiedTx);
            }
        }
    
        public override void AddToBlock(TxMempoolEntry mempoolEntry)
        {
            TxOut smartContractTxOut = mempoolEntry.Transaction.TryGetSmartContractTxOut();
            if (smartContractTxOut == null)
            {
                this.logger.LogDebug("Transaction does not contain smart contract information.");
                return;
            }

            this.logger.LogDebug("Transaction contains smart contract information.");

            this.ExecuteSmartContract(mempoolEntry);

            this.Block.AddTransaction(mempoolEntry.Transaction);

            this.UpdateBlockStatistics(mempoolEntry);
        }

        public override void UpdateHeaders()
        {
            this.UpdateBaseHeaders();

            this.Block.Header.Bits = this.Block.Header.GetWorkRequired(this.Network, this.ChainTip);

            var scHeader = (ISmartContractBlockHeader)this.Block.Header;

            scHeader.HashStateRoot = new uint256(this.stateSnapshot.Root);

            this.UpdateReceiptRoot(scHeader);

            this.UpdateLogsBloom(scHeader);
        }

        /// <summary>
        /// Sets the receipt root based on all the receipts generated in smart contract execution inside this block.
        /// </summary>
        /// <param name="scHeader">The smart contract header that will be updated.</param>
        private void UpdateReceiptRoot(ISmartContractBlockHeader scHeader)
        {
            var leaves = this.receipts.Select(r => r.GetHash()).ToList();
            scHeader.ReceiptRoot = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out _);
        }

        /// <summary>
        /// Sets the bloom filter for all logs that occurred in this block's execution.
        /// </summary>
        /// <param name="scHeader">The smart contract header that will be updated.</param>
        private void UpdateLogsBloom(ISmartContractBlockHeader scHeader)
        {
            Bloom logsBloom = new Bloom();
            foreach (Receipt receipt in this.receipts)
            {
                logsBloom.Or(receipt.Bloom);
            }
            scHeader.LogsBloom = logsBloom;
        }

        /// <summary>
        /// Execute the contract and add all relevant fees and refunds to the block.
        /// </summary>
        /// <remarks>TODO: At some point we need to change height to a ulong.</remarks>
        /// <param name="mempoolEntry">The mempool entry containing the smart contract transaction.</param>
        private IContractExecutionResult ExecuteSmartContract(TxMempoolEntry mempoolEntry)
        {
            // This coinview object can be altered by consensus whilst we're mining.
            // If this occurred, we would be mining on top of the wrong tip anyway, so
            // it's okay to throw a ConsensusError which is handled by the miner, and continue.

            GetSenderResult getSenderResult = this.senderRetriever.GetSender(mempoolEntry.Transaction, this.coinView, this.inBlock.Select(x => x.Transaction).ToList());
            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-block-assembler-addcontracttoblock", getSenderResult.Error));

            IContractTransactionContext transactionContext = new ContractTransactionContext((ulong)this.height, this.coinbaseAddress, mempoolEntry.Fee, getSenderResult.Sender, mempoolEntry.Transaction);
            IContractExecutor executor = this.executorFactory.CreateExecutor(this.stateSnapshot, transactionContext);
            IContractExecutionResult result = executor.Execute(transactionContext);
            Result<ContractTxData> deserializedCallData = this.callDataSerializer.Deserialize(transactionContext.Data);

            // Store all fields. We will reuse these in CoinviewRule.
            var receipt = new Receipt(
                new uint256(this.stateSnapshot.Root),
                result.GasConsumed,
                result.Logs.ToArray(),
                transactionContext.TransactionHash,
                transactionContext.Sender,
                result.To,
                result.NewContractAddress,
                !result.Revert,
                result.Return?.ToString(),
                result.ErrorMessage,
                deserializedCallData.Value.GasPrice,
                transactionContext.TxOutValue,
                deserializedCallData.Value.IsCreateContract ? null : deserializedCallData.Value.MethodName);

            this.receipts.Add(receipt);

            return result;
        }
    }
}
