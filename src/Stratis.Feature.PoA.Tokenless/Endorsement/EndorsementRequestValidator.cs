﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

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
        private readonly IEndorsements endorsements;

        public EndorsementRequestValidator(IEnumerable<IMempoolRule> mempoolRules, ILoggerFactory loggerFactory, IEndorsements endorsements)
        {
            this.mempoolRules = mempoolRules;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.endorsements = endorsements;
        }

        public bool ValidateRequest(EndorsementRequest request)
        {
            uint256 proposalId = request.ContractTransaction.GetHash();
            if (this.endorsements.GetEndorsement(proposalId) != null)
                return false;

            var mempoolContext = new MempoolValidationContext(request.ContractTransaction, new MempoolValidationState(false));

            try
            {
                foreach (IMempoolRule rule in this.mempoolRules)
                {
                    rule.CheckTransaction(mempoolContext);
                }

            }
            catch (Exception e) when (e is ConsensusErrorException || e is MempoolErrorException)
            {
                // TODO: Ideally this would only throw one type of error - MempoolErrorException. Alas, some code is shared with the consensus rules.
                this.logger.LogWarning("Endorsement request validation failed. Exception={0}", e.ToString());
                return false;
            }

            return true;
        }

    }
}
