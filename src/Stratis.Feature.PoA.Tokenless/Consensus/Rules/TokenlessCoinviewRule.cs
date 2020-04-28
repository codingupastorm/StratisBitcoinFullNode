﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Consensus.Rules.CommonRules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public sealed class TokenlessCoinviewRule : CoinViewRule
    {
        private BlockExecutionResultModel cachedResults;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IContractExecutorFactory executorFactory;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly ILogger logger;
        private IStateRepositoryRoot mutableStateRepository;
        private readonly IList<Receipt> receipts;
        private readonly List<uint256> privateDataRwsHashes;
        private readonly IReceiptRepository receiptRepository;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly IReadWriteSetTransactionSerializer rwsSerializer;
        private readonly IReadWriteSetValidator rwsValidator;
        private readonly IPrivateDataRetriever privateDataRetriever;

        public TokenlessCoinviewRule(
            ICallDataSerializer callDataSerializer,
            IBlockExecutionResultCache executionCache,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            IReceiptRepository receiptRepository,
            IStateRepositoryRoot stateRepositoryRoot,
            ITokenlessSigner tokenlessSigner, 
            IReadWriteSetTransactionSerializer rwsSerializer,
            IReadWriteSetValidator rwsValidator,
            IPrivateDataRetriever privateDataRetriever)
        {
            this.callDataSerializer = callDataSerializer;
            this.executorFactory = executorFactory;
            this.executionCache = executionCache;
            this.receipts = new List<Receipt>();
            this.privateDataRwsHashes = new List<uint256>();
            this.receiptRepository = receiptRepository;
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.tokenlessSigner = tokenlessSigner;
            this.rwsSerializer = rwsSerializer;
            this.rwsValidator = rwsValidator;
            this.privateDataRetriever = privateDataRetriever;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            this.logger.LogDebug("Block to validate '{0}'.", context.ValidationContext.BlockToValidate.GetHash());

            this.receipts.Clear();
            this.privateDataRwsHashes.Clear();

            this.DetermineIfResultIsAlreadyCached(context);
            await this.ProcessTransactionsAsync(context);
            this.CompareAndValidateStateRoot(context);
            this.ValidateAndStoreReceipts(context);
            this.ValidateLogsBloom(context);

            // Push to underlying database
            this.mutableStateRepository.Commit();

            // Move the private data to the "actual" private state database.
            this.privateDataRetriever.MoveDataFromTransientToPrivateStore(this.privateDataRwsHashes);

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

            this.logger.LogDebug("Compare state roots '{0}' and '{1}'.", mutableStateRepositoryRoot, blockHeaderHashStateRoot);

            if (mutableStateRepositoryRoot != blockHeaderHashStateRoot)
                SmartContractConsensusErrors.UnequalStateRoots.Throw();
        }

        private async Task ProcessTransactionsAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return;

            if (this.cachedResults != null)
            {
                this.logger.LogDebug("Skipping block '{0}' as the result is already in the cache.", context.ValidationContext.BlockToValidate.GetHash());
                return;
            }

            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions.Where(x => !x.IsCoinBase))
            {
                this.logger.LogDebug("Processing transaction '{0}'.", transaction.GetHash());

                this.ValidateSubmittedTransaction(transaction);

                if (transaction.Outputs.First().ScriptPubKey.IsReadWriteSet())
                {
                    await this.ExecuteReadWriteTransactionAsync(context.ValidationContext, transaction);
                    continue;
                }

                TxOut smartContractTxOut = transaction.Outputs.FirstOrDefault(txOut => SmartContractScript.IsSmartContractExec(txOut.ScriptPubKey));
                if (smartContractTxOut == null)
                {
                    this.logger.LogDebug("'{0}' contains no smart contract information.", transaction.GetHash());
                    continue;
                }

                this.ExecuteContractTransaction(context.ValidationContext, transaction);
            }
        }

        private async Task ExecuteReadWriteTransactionAsync(ValidationContext validationContext, Transaction transaction)
        {
            // Apply RWS to the state repository.
            ReadWriteSet rws = this.rwsSerializer.GetReadWriteSet(transaction);

            if (!this.rwsValidator.IsReadWriteSetValid(this.mutableStateRepository, rws))
            {
                // TODO: Discard block if this happens
                throw new NotImplementedException("Do we discard transactions if they are no longer valid by version?");
            }

            int blockHeight = validationContext.ChainedHeaderToValidate.Height;
            int txIndex = validationContext.BlockToValidate.Transactions.IndexOf(transaction);

            string version = $"{blockHeight}.{txIndex}"; // TODO: Componentise retrieving version?

            if (rws.Writes.Any(x => x.IsPrivateData))
            {
                if (await this.privateDataRetriever.WaitForPrivateDataIfRequired(rws))
                {
                    // If we did get the private data, as it is applicable to us, add the private data RWS to be committed on block commit.
                    this.privateDataRwsHashes.Add(rws.GetHash());
                }
            }

            this.rwsValidator.ApplyReadWriteSet(this.mutableStateRepository, rws, version);
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
        /// <param name="validationContext">The instance containing the information required to do the validation.</param>
        /// <param name="transaction">The submitted transaction to validate.</param>
        private void ExecuteContractTransaction(ValidationContext validationContext, Transaction transaction)
        {
            this.logger.LogDebug("Processing smart contract transaction '{0}'.", transaction.GetHash());

            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
            if (!getSenderResult.Success)
                throw new ConsensusErrorException(new ConsensusError("sc-consensusvalidator-executecontracttransaction-sender", getSenderResult.Error));

            ulong txIndex = (ulong) validationContext.BlockToValidate.Transactions.IndexOf(transaction);
            IContractTransactionContext transactionContext = new ContractTransactionContext(
                (ulong)validationContext.ChainedHeaderToValidate.Height,
                txIndex,
                new uint160(0),
                getSenderResult.Sender,
                transaction,
                null); // Contracts executed inside blocks will never have transient data. 

            IContractExecutor executor = this.executorFactory.CreateExecutor(this.mutableStateRepository);
            Result<ContractTxData> deserializedCallData = this.callDataSerializer.Deserialize(transactionContext.Data);
            IContractExecutionResult result = executor.Execute(transactionContext);

            var receipt = new Receipt(
                new uint256(this.mutableStateRepository.Root),
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
                result.ReadWriteSet.GetReadWriteSet().ToJson(),
                deserializedCallData.Value.IsCreateContract ? null : deserializedCallData.Value.MethodName,
                transactionContext.BlockHeight)
            {
                BlockHash = validationContext.BlockToValidate.GetHash()
            };

            this.receipts.Add(receipt);

            this.logger.LogDebug("Processing smart contract transaction '{0}' done.", transaction.GetHash());
        }

        /// <summary>
        /// Throws a consensus exception if the receipt roots don't match.
        /// </summary>
        /// <param name="context">The instance containing the information required to do the validation.</param>
        private void ValidateAndStoreReceipts(RuleContext context)
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

        /// <inheritdoc/>
        protected override bool CheckInput(Transaction tx, int inputIndexCopy, TxOut txout, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        protected override Money GetTransactionFee(UnspentOutputSet view, Transaction tx)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override Money GetProofOfWorkReward(int height)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }
    }
}
