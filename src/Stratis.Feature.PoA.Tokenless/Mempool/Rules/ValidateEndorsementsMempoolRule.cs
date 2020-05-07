using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    public class ValidateEndorsementsMempoolRule : MempoolRule
    {
        public static Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, Func<Transaction, string>> ErrorMessages = new Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, Func<Transaction, string>>
        {
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall, tx => "" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, tx =>  $"Transaction '{tx.GetHash()}' contained a contract transaction but one or more of its endorsements were malformed" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid, tx => $"Transaction '{tx.GetHash()}' contained a contract transaction but the endorsement policy was not satisfied" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid, tx => $"Transaction '{tx.GetHash()}' contained a contract transaction but one or more of its endorsements contained invalid signatures" }
        };

        public static Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, int> MempoolErrorTypes = new Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, int>
        {
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, MempoolErrors.RejectMalformed },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid, MempoolErrors.RejectInvalid },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid, MempoolErrors.RejectInvalid }
        };

        private readonly EndorsedContractTransactionValidationRule rule;

        public ValidateEndorsementsMempoolRule(EndorsedContractTransactionValidationRule rule, Network network, ITxMempool mempool, MempoolSettings settings, ChainIndexer chainIndexer, ILoggerFactory loggerFactory) 
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.rule = rule;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            (bool valid, EndorsedContractTransactionValidationRule.EndorsementValidationErrorType error) = this.rule.CheckTransaction(context.Transaction);

            if (valid)
            {
                return;
            }

            // Special case, exists just for the logging.
            if (error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall)
            {
                // Don't reject, just ignore.
                this.logger.LogDebug($"{context.Transaction.GetHash()}' does not contain a contract call.");
                return;
            }

            var errorMessage = ErrorMessages[error](context.Transaction);
            var errorType = EndorsedContractTransactionValidationRule.ErrorMessages[error];
            var mempoolError = MempoolErrorTypes[error];

            context.State.Fail(new MempoolError(mempoolError, errorType), errorMessage).Throw();
        }
    }
}