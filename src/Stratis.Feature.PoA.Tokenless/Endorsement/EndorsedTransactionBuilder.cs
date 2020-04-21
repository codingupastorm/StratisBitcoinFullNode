using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    /// <summary>
    /// Builds a transaction that has satisfied an endorsement policy.
    /// </summary>
    public class EndorsedTransactionBuilder
    {
        private readonly IEndorsementSigner endorsementSigner;

        public EndorsedTransactionBuilder(IEndorsementSigner endorsementSigner)
        {
            this.endorsementSigner = endorsementSigner;
        }

        public Transaction Build(List<SignedProposalResponse> proposalResponses)
        {
            var transaction = new Transaction();
            
            // Endorsement signer uses the same logic to sign the first input with the transaction signing key.
            this.endorsementSigner.Sign(transaction);

            return transaction;
        }
    }
}
