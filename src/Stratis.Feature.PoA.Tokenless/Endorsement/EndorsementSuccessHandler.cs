using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSuccessHandler
    {
        Task<bool> ProcessEndorsement(Transaction signedRWSTransaction);
    }

    public class EndorsementSuccessHandler : IEndorsementSuccessHandler
    {
        private readonly IBroadcasterManager broadcasterManager;

        public EndorsementSuccessHandler(IBroadcasterManager broadcasterManager)
        {
            this.broadcasterManager = broadcasterManager;
        }

        public async Task<bool> ProcessEndorsement(Transaction signedRWSTransaction)
        {
            // TODO: Recruit multiple endorsements before broadcasting the transactions.

            await this.broadcasterManager.BroadcastTransactionAsync(signedRWSTransaction);

            return true;
        }
    }
}
