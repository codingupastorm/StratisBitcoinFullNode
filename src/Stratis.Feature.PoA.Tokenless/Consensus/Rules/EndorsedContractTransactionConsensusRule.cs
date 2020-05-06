using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public class EndorsedContractTransactionConsensusRule : PartialValidationConsensusRule
    {
        private readonly EndorsedContractTransactionValidationRule rule;

        public static Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, string> ErrorMessages = new Dictionary<EndorsedContractTransactionValidationRule.EndorsementValidationErrorType, string>
        {
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall, "" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, "malformed endorsements" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.PolicyInvalid, "endorsement policy not satisfied" },
            { EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid, "endorsement policy signatures invalid" }
        };

        public EndorsedContractTransactionConsensusRule(EndorsedContractTransactionValidationRule rule)
        {
            this.rule = rule;
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                (bool valid, EndorsedContractTransactionValidationRule.EndorsementValidationErrorType error) = this.rule.CheckTransaction(transaction);

                if (valid || error == EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall)
                {
                    // No further validation needed.
                    continue;
                }

                var errorType = EndorsedContractTransactionValidationRule.ErrorMessages[error];
                var errorMessage = ErrorMessages[error];

                new ConsensusError(errorType, errorMessage).Throw();
            }

            return Task.CompletedTask;
        }
    }
}