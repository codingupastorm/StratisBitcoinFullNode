using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    /// <summary>
    /// Builds a transaction that has satisfied an endorsement policy.
    /// </summary>
    public class EndorsedTransactionBuilder
    {
        public Transaction Build(List<SignedProposalResponse> proposalResponses)
        {
            throw new NotImplementedException();
        }
    }
}
