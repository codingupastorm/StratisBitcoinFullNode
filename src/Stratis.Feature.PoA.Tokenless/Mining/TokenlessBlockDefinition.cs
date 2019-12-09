using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
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
        private uint160 coinbaseAddress;
        private readonly ICoinView coinView;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ILogger logger;
        private readonly List<TxOut> refundOutputs;
        private readonly List<Receipt> receipts;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly ICallDataSerializer callDataSerializer;
        private IStateRepositoryRoot stateSnapshot;
        private readonly ISenderRetriever senderRetriever;
        private ulong blockGasConsumed;

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
            this.refundOutputs = new List<TxOut>();
            this.receipts = new List<Receipt>();

            // When building smart contract blocks, we will be generating and adding both transactions to the block and txouts to the coinbase. 
            // At the moment, these generated objects aren't accounted for in the block size and weight accounting. 
            // This means that if blocks started getting full, this miner could start generating blocks greater than the max consensus block size.
            // To avoid this without significantly overhauling the BlockDefinition, for now we just lower the block size by a percentage buffer.
            // If in the future blocks are being built over the size limit and you need an easy fix, just increase the size of this buffer.
            this.Options = blockBufferGenerator.GetOptionsWithBuffer(this.Options);
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

            if (this.blockGasConsumed >= SmartContractBlockDefinition.GasPerBlockLimit)
            {
                this.logger.LogDebug("The gas limit for this block has been reached.");
                return;
            }

            IContractExecutionResult result = this.ExecuteSmartContract(mempoolEntry);

            // If including this transaction would put us over the block gas limit, then don't include it
            // and roll back all of the execution we did.
            //if (this.blockGasConsumed > GasPerBlockLimit)
            //{
            //    // Remove the last receipt.
            //    this.receipts.RemoveAt(this.receipts.Count - 1);

            //    // Set our state to where it was before this execution.
            //    uint256 lastState = this.receipts.Last().PostState;
            //    this.stateSnapshot.SyncToRoot(lastState.ToBytes());

            //    return;
            //}

            this.AddTransactionToBlock(mempoolEntry.Transaction);
            this.UpdateBlockStatistics(mempoolEntry);
            this.UpdateTotalFees(result.Fee);

            // If there are refunds, add them to the block.
            //if (result.Refund != null)
            //{
            //    this.refundOutputs.Add(result.Refund);
            //    this.logger.LogDebug("refund was added with value {0}.", result.Refund.Value);
            //}

            // Add internal transactions made during execution.
            if (result.InternalTransaction != null)
            {
                this.AddTransactionToBlock(result.InternalTransaction);
                this.logger.LogDebug("Internal {0}:{1} was added.", nameof(result.InternalTransaction), result.InternalTransaction.GetHash());
            }
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            throw new System.NotImplementedException();
        }

        public override void UpdateHeaders()
        {
            throw new System.NotImplementedException();
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

            this.blockGasConsumed += result.GasConsumed;

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
