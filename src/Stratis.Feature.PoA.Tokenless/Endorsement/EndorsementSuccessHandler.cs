using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Store;

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
        private readonly ITransientStore transientStore;
        private readonly IConsensusManager consensus;

        public EndorsementSuccessHandler(IBroadcasterManager broadcasterManager, IEndorsements endorsements, IEndorsedTransactionBuilder endorsedTransactionBuilder, ITransientStore transientStore, IConsensusManager consensus)
        {
            this.broadcasterManager = broadcasterManager;
            this.endorsements = endorsements;
            this.endorsedTransactionBuilder = endorsedTransactionBuilder;
            this.transientStore = transientStore;
            this.consensus = consensus;
        }

        public async Task<bool> ProcessEndorsementAsync(uint256 proposalId, SignedProposalResponse signedProposalResponse, INetworkPeer peer)
        {
            EndorsementInfo info = this.endorsements.GetEndorsement(proposalId);

            if (info == null) 
                return false;

            // Check public and private RWSs match
            if (!signedProposalResponse.ValidateReadWriteSets())
                return false;

            X509Certificate certificate = (peer.Connection as TlsEnabledNetworkPeerConnection).GetPeerCertificate();

            if (!info.AddSignature(certificate, signedProposalResponse))
                return false;

            // If the policy has been satisfied, this will return true and we can broadcast the signed transaction.
            if (info.Validate())
            {
                IReadOnlyList<SignedProposalResponse> validProposalResponses = info.GetValidProposalResponses();

                Transaction endorsedTx = this.endorsedTransactionBuilder.Build(validProposalResponses);

                await this.broadcasterManager.BroadcastTransactionAsync(endorsedTx);

                // We can choose any RWS here, they should all be in agreeance.
                ReadWriteSet privateReadWriteSetData = validProposalResponses.First().PrivateReadWriteSet;

                uint blockHeight = (uint)this.consensus.Tip.Height + 1;

                this.transientStore.Persist(signedProposalResponse.ProposalResponse.GetHash(), blockHeight, new TransientStorePrivateData(privateReadWriteSetData.ToJsonEncodedBytes()));

                return true;
            }

            return false;
        }
    }
}
