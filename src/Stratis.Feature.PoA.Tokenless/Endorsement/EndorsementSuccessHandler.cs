using System.Threading.Tasks;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSuccessHandler
    {
        Task<bool> ProcessEndorsementAsync(uint256 proposalId, SignedProposalResponse signedProposalResponse);
    }

    public class EndorsementSuccessHandler : IEndorsementSuccessHandler
    {
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IEndorsements endorsements;

        public EndorsementSuccessHandler(IBroadcasterManager broadcasterManager, IEndorsements endorsements)
        {
            this.broadcasterManager = broadcasterManager;
            this.endorsements = endorsements;
        }

        public async Task<bool> ProcessEndorsementAsync(uint256 proposalId, SignedProposalResponse signedProposalResponse)
        {
            // TODO: Recruit multiple endorsements before broadcasting the transactions.

            EndorsementInfo info = this.endorsements.GetEndorsement(proposalId);
            
            if (info != null)
            {
                // Add the signature org + address to the policy state.
                info.AddSignature(null); // TODO

                // If the policy has been satisfied, this will return true and we can broadcast the signed transaction.

                if (info.Validate())
                {
                    // TODO build the endorsed transaction with the txins of all the endorsers.
                    //await this.broadcasterManager.BroadcastTransactionAsync(finalTransactionWithEndorsements);
                    return true;
                }
            }

            return false;
        }
    }
}
