using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    public class ValidateEndorsementsMempoolRule : MempoolRule
    {
        private readonly EndorsedContractTransactionValidationRule rule;

        public ValidateEndorsementsMempoolRule(IEndorsementSignatureValidator endorsementSignatureValidator, IEndorsedTransactionBuilder endorsedTransactionBuilder, IEndorsementPolicyValidator policyValidator, Network network, ITxMempool mempool, MempoolSettings settings, ChainIndexer chainIndexer, ILoggerFactory loggerFactory) 
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.rule = new EndorsedContractTransactionValidationRule(endorsedTransactionBuilder, endorsementSignatureValidator, policyValidator);
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            (bool valid, EndorsedContractTransactionValidationRule.EndorsementValidationErrorType error) = this.rule.CheckTransaction(context.Transaction);
            var errorType = EndorsedContractTransactionValidationRule.ErrorMessages[error];

            if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall)
            {
                // Don't reject, just ignore.
                this.logger.LogDebug($"{context.Transaction.GetHash()}' does not contain a contract call.");
                return;
            }

            if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed)
            {
                var errorMessage = $"Transaction '{context.Transaction.GetHash()}' contained a contract transaction but one or more of its endorsements were malformed";

                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, errorType), errorMessage).Throw();
                return;
            }

            if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid)
            {
                var errorMessage = $"Transaction '{context.Transaction.GetHash()}' contained a contract transaction but the endorsement policy was not satisfied";

                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, errorType), errorMessage).Throw();
            }

            if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid)
            {
                var errorMessage = $"Transaction '{context.Transaction.GetHash()}' contained a contract transaction but one or more of its endorsements contained invalid signatures";

                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, errorType), errorMessage).Throw();
            }
        }
    }
}