﻿using System.Collections.Generic;
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

            this.stateSnapshot = this.stateRoot.GetSnapshotTo(((ISmartContractBlockHeader)this.ConsensusManager.Tip.Header).HashStateRoot.ToBytes());
            this.receipts.Clear();

            base.Configure();

            this.block = this.BlockTemplate.Block;

            this.MedianTimePast = Utils.DateTimeToUnixTime(this.ChainTip.GetMedianTimePast());
            this.LockTimeCutoff = MempoolValidator.StandardLocktimeVerifyFlags.HasFlag(Transaction.LockTimeFlags.MedianTimePast) ? this.MedianTimePast : this.BlockTemplate.Block.Header.Time;

            this.AddTransactions(out int _, out int _);

            this.logger.LogDebug("Serialized size is {0} bytes, block weight is {1}, number of txs is {2}", this.BlockTemplate.Block.GetSerializedSize(), this.BlockTemplate.Block.GetBlockWeight(this.Network.Consensus), this.BlockTx);

            this.UpdateHeaders();

            // Cache the results. We don't need to execute these again when validating.
            var cacheModel = new BlockExecutionResultModel(this.stateSnapshot, this.receipts);
            this.executionCache.StoreExecutionResult(this.BlockTemplate.Block.GetHash(), cacheModel);

            return this.BlockTemplate;
        }

        /// <inheritdoc/>
        protected override void Configure()
        {
            this.BlockSize = 1000;
            this.BlockTemplate = new BlockTemplate(this.Network);
            this.BlockTx = 0;
            this.BlockWeight = 1000 * this.Network.Consensus.Options.WitnessScaleFactor;
            this.inBlock = new TxMempool.SetEntries();
        }

        /// <inheritdoc/>
        protected override void AddTransactions(out int packagesSelected, out int descendentsUpdated)
        {
            packagesSelected = 0;
            descendentsUpdated = 0;

            // TODO-TL: Implement new ordering by time.
            var entriesToAdd = this.MempoolLock.ReadAsync(() => this.Mempool.MapTx.AncestorScore).ConfigureAwait(false).GetAwaiter().GetResult().ToList();

            foreach (TxMempoolEntry entryToAdd in entriesToAdd)
            {
                // Skip entries in mapTx that are already in a block.
                if (this.inBlock.Contains(entryToAdd))
                    continue;

                if (!this.TestPackage(entryToAdd, entryToAdd.SizeWithAncestors, 0))
                    continue;

                if (!this.TestPackageTransactions(new TxMempool.SetEntries() { entryToAdd }))
                    continue;

                this.AddToBlock(entryToAdd);
            }
        }

        /// <inheritdoc/>
        protected override bool TestPackage(TxMempoolEntry entry, long packageSize, long packageSigOpsCost)
        {
            if (this.BlockWeight + this.Network.Consensus.Options.WitnessScaleFactor * packageSize >= this.Options.BlockMaxWeight)
            {
                this.logger.LogTrace("(-)[MAX_WEIGHT_REACHED]:false");
                return false;
            }

            this.logger.LogTrace("(-):true");

            return true;
        }

        /// <inheritdoc/>
        protected override bool TestPackageTransactions(TxMempool.SetEntries entries)
        {
            if (!this.NeedSizeAccounting)
                return true;

            foreach (TxMempoolEntry entry in entries)
            {
                if (this.BlockSize + entry.Transaction.GetSerializedSize() >= this.Options.BlockMaxSize)
                    return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override void AddToBlock(TxMempoolEntry entry)
        {
            TxOut smartContractTxOut = entry.Transaction.TryGetSmartContractTxOut();
            if (smartContractTxOut == null)
                this.logger.LogDebug("Transaction {0} does not contain smart contract information.", entry.Transaction.GetHash());
            else
            {
                this.logger.LogDebug("Transaction {0} contains smart contract information.", entry.Transaction.GetHash());
                this.ExecuteSmartContract(entry);
            }

            this.AddTransactionToBlock(entry.Transaction);
            this.UpdateBlockStatistics(entry);
        }

        /// <inheritdoc/>
        public override void UpdateHeaders()
        {
            this.UpdateBaseHeaders();

            this.BlockTemplate.Block.Header.Bits = this.BlockTemplate.Block.Header.GetWorkRequired(this.Network, this.ChainTip);

            var scHeader = (ISmartContractBlockHeader)this.BlockTemplate.Block.Header;
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
            var logsBloom = new Bloom();
            foreach (Receipt receipt in this.receipts)
            {
                logsBloom.Or(receipt.Bloom);
            }
            scHeader.LogsBloom = logsBloom;
        }

        /// <summary>
        /// Execute the contract and add all relevant fees and refunds to the block.
        /// </summary>
        /// <param name="mempoolEntry">The mempool entry containing the smart contract transaction.</param>
        private IContractExecutionResult ExecuteSmartContract(TxMempoolEntry mempoolEntry)
        {
            // TODO: Get the sender from ITokenlessSigner
            var getSenderResult = GetSenderResult.CreateSuccess(new uint160(0));

            IContractTransactionContext transactionContext = new ContractTransactionContext((ulong)this.height, new uint160(0), mempoolEntry.Fee, getSenderResult.Sender, mempoolEntry.Transaction);
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
