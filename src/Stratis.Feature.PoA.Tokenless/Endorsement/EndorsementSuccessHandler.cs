using System.Threading.Tasks;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSuccessHandler
    {
        Task<bool> ProcessEndorsementAsync(uint256 proposalId, Transaction signedRWSTransaction);
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

        public async Task<bool> ProcessEndorsementAsync(uint256 proposalId, Transaction signedRWSTransaction)
        {
            // TODO: Recruit multiple endorsements before broadcasting the transactions.

            EndorsementInfo info = this.endorsements.GetEndorsement(proposalId);
            
            if (info != null)
            {
                // Add the signature org + address to the policy state.
                info.AddSignature(signedRWSTransaction);

                // If the policy has been satisfied, this will return true and we can broadcast the signed transaction.
                // TODO rebuild the broadcasted transaction with the txins of all the endorsers.
                if (info.Validate())
                {
                    await this.broadcasterManager.BroadcastTransactionAsync(signedRWSTransaction);
                    return true;
                }
            }

            return false;
        }
    }
}
