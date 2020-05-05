using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public class EndorsedContractTransactionConsensusRule : PartialValidationConsensusRule
    {
        private readonly EndorsedContractTransactionValidationRule rule;

        public EndorsedContractTransactionConsensusRule(EndorsedContractTransactionValidationRule rule)
        {
            this.rule = rule;
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                (bool valid, EndorsedContractTransactionValidationRule.EndorsementValidationErrorType error) = this.rule.CheckTransaction(transaction);

                var errorType = EndorsedContractTransactionValidationRule.ErrorMessages[error];

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall)
                {
                    // No further validation needed.
                    continue;
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed)
                {
                    new ConsensusError(errorType, "malformed endorsements").Throw();
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid)
                {
                    new ConsensusError(errorType, "endorsement policy not satisfied").Throw();
                }

                if (!valid && error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid)
                {
                    new ConsensusError(errorType, "endorsement policy signatures invalid").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}