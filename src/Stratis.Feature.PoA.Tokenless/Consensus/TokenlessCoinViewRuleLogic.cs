using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    public sealed class TokenlessCoinViewRuleLogic
    {
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly List<Transaction> blockTxsProcessed;
        private BlockExecutionResultModel cachedResults;
        private readonly IList<Receipt> receipts;
        private IStateRepositoryRoot mutableStateRepository;
        private readonly ILogger logger;

        public TokenlessCoinViewRuleLogic(
            ICallDataSerializer callDataSerializer,
            ICoinView coinView,
            IBlockExecutionResultCache executionCache,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            IReceiptRepository receiptRepository,
            ISenderRetriever senderRetriever,
            IStateRepositoryRoot stateRepositoryRoot)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
            this.executionCache = executionCache;
            this.blockTxsProcessed = new List<Transaction>();
            this.receipts = new List<Receipt>();
            this.logger = loggerFactory.CreateLogger<SmartContractCoinViewRuleLogic>();
        }

        public async Task RunAsync(RuleContext context)
        {
            this.logger.LogDebug("Block to validate '{0}'", context.ValidationContext.BlockToValidate.GetHash());

            this.blockTxsProcessed.Clear();
            this.receipts.Clear();

            this.DetermineIfResultIsAlreadyCached(context);
            this.ProcessTransactions(context);
            this.CompareAndValidateStateRoot(context);
            this.ValidateAndStoreReceipts(context);
            this.ValidateLogsBloom(context);

            // Push to underlying database
            this.mutableStateRepository.Commit();

            // Update the globally injected state so all services receive the updates.
            this.stateRepositoryRoot.SyncToRoot(this.mutableStateRepository.Root);
        }

        private void DetermineIfResultIsAlreadyCached(RuleContext context)
        {
            // Get a IStateRepositoryRoot we can alter without affecting the injected one which is used elsewhere.
            uint256 blockRoot = ((ISmartContractBlockHeader)context.ValidationContext.ChainedHeaderToValidate.Previous.Header).HashStateRoot;
            this.logger.LogDebug("Block hash state root '{0}'.", blockRoot);

            this.cachedResults = this.executionCache.GetExecutionResult(context.ValidationContext.BlockToValidate.GetHash());
            if (this.cachedResults == null)
            {
                // We have no cached results. Didn't come from our miner. We execute the contracts, so need to set up a new state repository.
                this.mutableStateRepository = this.stateRepositoryRoot.GetSnapshotTo(blockRoot.ToBytes());
            }
            else
            {
                // We have already done all of this execution when mining so we will use those results.
                this.mutableStateRepository = this.cachedResults.MutatedStateRepository;

                foreach (Receipt receipt in this.cachedResults.Receipts)
                {
                    // Block hash needs to be set for all. It was set during mining and can only be updated after.
                    receipt.BlockHash = context.ValidationContext.BlockToValidate.GetHash();
                    this.receipts.Add(receipt);
                }
            }
        }

        private void CompareAndValidateStateRoot(RuleContext context)
        {
            var smartContractBlockHeader = (ISmartContractBlockHeader)context.ValidationContext.BlockToValidate.Header;
            var mutableStateRepositoryRoot = new uint256(this.mutableStateRepository.Root);
            uint256 blockHeaderHashStateRoot = smartContractBlockHeader.HashStateRoot;

            this.logger.LogDebug("Compare state roots '{0}' and '{1}'", mutableStateRepositoryRoot, blockHeaderHashStateRoot);

            if (mutableStateRepositoryRoot != blockHeaderHashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();
        }

        private void ProcessTransactions(RuleContext context)
        {
            if (context.SkipValidation)
                return;

            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                // We already have results for this block therefore no need to do any processing.
                if (this.cachedResults != null)
                {
                    // As we are in a tokenless blockchain, there is no need to call base's UpdateUtxOSet as we aren't "spending" anything.
                    this.blockTxsProcessed.Add(transaction);
                    return;
                }

                // If we are here, was definitely submitted by someone.
                this.ValidateSubmittedTransaction(transaction);

                TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => SmartContractScript.IsSmartContractExec(txOut.ScriptPubKey));
                if (smartContractTxOut != null)
                    this.ExecuteContractTransaction(context, transaction);

                // As we are in a tokenless blockchain, there is no need to call base's UpdateUtxOSet as we aren't "spending" anything.
                this.blockTxsProcessed.Add(transaction);
            }
        }

        /// <summary>
        /// Validates that a submitted transaction doesn't contain illegal operations.
        /// </summary>
        /// <param name="transaction">The submitted transaction to validate.</param>
        public void ValidateSubmittedTransaction(Transaction transaction)
        {
            if (transaction.Inputs.Any(x => x.ScriptSig.IsSmartContractSpend()))
                SmartContractConsensusErrors.UserOpSpend.Throw();

            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractInternalCall()))
                SmartContractConsensusErrors.UserInternalCall.Throw();
        }

        /// <summary>
        /// Executes the smart contract part of a transaction
        /// </summary>
        public void ExecuteContractTransaction(RuleContext context, Transaction transaction)
        {
            IContractTransactionContext txContext = this.GetSmartContractTransactionContext(context, transaction);
            IContractExecutor executor = this.executorFactory.CreateExecutor(this.mutableStateRepository, txContext);
            Result<ContractTxData> deserializedCallData = this.callDataSerializer.Deserialize(txContext.Data);

            IContractExecutionResult result = executor.Execute(txContext);

            var receipt = new Receipt(
                new uint256(this.mutableStateRepository.Root),
                result.GasConsumed,
                result.Logs.ToArray(),
                txContext.TransactionHash,
                txContext.Sender,
                result.To,
                result.NewContractAddress,
                !result.Revert,
                result.Return?.ToString(),
                result.ErrorMessage,
                deserializedCallData.Value.GasPrice,
                txContext.TxOutValue,
                deserializedCallData.Value.IsCreateContract ? null : deserializedCallData.Value.MethodName,
                txContext.BlockHeight)
            {
                BlockHash = context.ValidationContext.BlockToValidate.GetHash()
            };

            this.receipts.Add(receipt);
        }

        /// <summary>
        /// Retrieves the context object to be given to the contract executor.
        /// </summary>
        public IContractTransactionContext GetSmartContractTransactionContext(RuleContext context, Transaction transaction)
        {
            ulong blockHeight = Convert.ToUInt64(context.ValidationContext.ChainedHeaderToValidate.Height);

            GetSenderResult getSenderResult = this.senderRetriever.GetSender(transaction, this.coinView, this.blockTxsProcessed);

            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            Script coinbaseScriptPubKey = context.ValidationContext.BlockToValidate.Transactions[0].Outputs[0].ScriptPubKey;

            GetSenderResult getCoinbaseResult = this.senderRetriever.GetAddressFromScript(coinbaseScriptPubKey);

            uint160 coinbaseAddress = (getCoinbaseResult.Success) ? getCoinbaseResult.Sender : uint160.Zero;

            Money mempoolFee = transaction.GetFee(((UtxoRuleContext)context).UnspentOutputSet);

            return new ContractTransactionContext(blockHeight, coinbaseAddress, mempoolFee, getSenderResult.Sender, transaction);
        }

        /// <summary>
        /// Throws a consensus exception if the receipt roots don't match.
        /// </summary>
        public void ValidateAndStoreReceipts(RuleContext context)
        {
            var smartContractBlockHeader = (ISmartContractBlockHeader)context.ValidationContext.BlockToValidate.Header;

            var leaves = this.receipts.Select(x => x.GetHash()).ToList();
            uint256 expectedReceiptRoot = BlockMerkleRootRule.ComputeMerkleRoot(leaves, out _);

            if (smartContractBlockHeader.ReceiptRoot != expectedReceiptRoot)
                SmartContractConsensusErrors.UnequalReceiptRoots.Throw();

            this.receiptRepository.Store(this.receipts);
        }

        private void ValidateLogsBloom(RuleContext context)
        {
            var smartContractBlockHeader = (ISmartContractBlockHeader)context.ValidationContext.BlockToValidate.Header;

            var logsBloom = new Bloom();
            foreach (Receipt receipt in this.receipts)
            {
                logsBloom.Or(receipt.Bloom);
            }

            if (logsBloom != smartContractBlockHeader.LogsBloom)
                SmartContractConsensusErrors.UnequalLogsBloom.Throw();
        }

        public bool CheckInput(TxOut txout, TxIn input)
        {
            if (txout.ScriptPubKey.IsSmartContractExec() || txout.ScriptPubKey.IsSmartContractInternalCall())
                return input.ScriptSig.IsSmartContractSpend();

            // TODO-TL: Do we need to check the new TxIn signature?
            return true;
        }
    }
}
