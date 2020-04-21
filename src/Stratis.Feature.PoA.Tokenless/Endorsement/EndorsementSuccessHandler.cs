using System.Threading.Tasks;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{

    public interface IEndorsementSuccessHandler
    {
        Task<bool> ProcessEndorsementAsync(uint256 proposalId, SignedProposalResponse signedProposalResponse,
            INetworkPeer peer);
    }

    public class EndorsementSuccessHandler : IEndorsementSuccessHandler
    {
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IEndorsements endorsements;
        private readonly IEndorsedTransactionBuilder endorsedTransactionBuilder;

        public EndorsementSuccessHandler(IBroadcasterManager broadcasterManager, IEndorsements endorsements, IEndorsedTransactionBuilder endorsedTransactionBuilder)
        {
            this.broadcasterManager = broadcasterManager;
            this.endorsements = endorsements;
            this.endorsedTransactionBuilder = endorsedTransactionBuilder;
        }

        public async Task<bool> ProcessEndorsementAsync(uint256 proposalId, SignedProposalResponse signedProposalResponse, INetworkPeer peer)
        {
            EndorsementInfo info = this.endorsements.GetEndorsement(proposalId);

            if (info == null) 
                return false;

            X509Certificate certificate = (peer.Connection as TlsEnabledNetworkPeerConnection).GetPeerCertificate();

            if (!info.AddSignature(certificate, signedProposalResponse))
                return false;

            // TODO: Recruit multiple endorsements before broadcasting the transactions.
            // If the policy has been satisfied, this will return true and we can broadcast the signed transaction.
            if (info.Validate())
            {
                Transaction endorsedTx = this.endorsedTransactionBuilder.Build(null); // TODO

                await this.broadcasterManager.BroadcastTransactionAsync(endorsedTx);
                return true;
            }

            return false;
        }
    }
}
