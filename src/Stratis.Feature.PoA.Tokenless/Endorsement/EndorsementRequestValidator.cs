using System;
using System.Collections.Generic;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestValidator
    {
        bool ValidateRequest(EndorsementRequest request);
    }

    public class EndorsementRequestValidator : IEndorsementRequestValidator
    {
        /*
         * From HL docs:
         * (1) that the transaction proposal is well formed,
         * (2) it has not been submitted already in the past (replay-attack protection),
         * (3) the signature is valid (using the MSP), and
         * (4) that the submitter (Client A, in the example) is properly authorized to perform the proposed operation on that channel (namely, each endorsing peer ensures that the submitter satisfies the channel’s Writers policy)
         *
         *
         * Covered by:
         * (1) IsSmartContractWellFormedMempoolRule
         * (2) NoDuplicateTransactionExistOnChainMempoolRule
         * (3) SenderInputMempoolRule
         * (4) SenderInputMempoolRule
         *
         */

        private readonly IEnumerable<IMempoolRule> mempoolRules;

        public EndorsementRequestValidator(IEnumerable<IMempoolRule> mempoolRules)
        {
            this.mempoolRules = mempoolRules;
        }

        public bool ValidateRequest(EndorsementRequest request)
        {
            // TODO: Catch thrown errors.

            throw new NotImplementedException("Check transaction vs consensus rules and rules above.");
        }

    }
}
