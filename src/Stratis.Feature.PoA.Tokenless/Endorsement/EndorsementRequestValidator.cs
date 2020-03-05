using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus;
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
        private readonly ILogger logger;

        public EndorsementRequestValidator(IEnumerable<IMempoolRule> mempoolRules, ILoggerFactory loggerFactory)
        {
            this.mempoolRules = mempoolRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool ValidateRequest(EndorsementRequest request)
        {
            try
            {
                // TODO: Run rules.
            }
            catch (ConsensusErrorException e)
            {
                this.logger.LogWarning("Endorsement request validation failed. Exception={0}", e.ToString());
                return false;
            }

            throw new NotImplementedException("Check transaction vs consensus rules and rules above.");
        }

    }
}
