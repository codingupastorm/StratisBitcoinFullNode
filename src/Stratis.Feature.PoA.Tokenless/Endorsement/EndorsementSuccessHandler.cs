using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;

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
                info.SetState(EndorsementState.Approved);

                await this.broadcasterManager.BroadcastTransactionAsync(signedRWSTransaction);

                return true;
            }

            return false;
        }
    }
}
