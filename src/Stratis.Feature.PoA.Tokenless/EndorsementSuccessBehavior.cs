using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Payloads;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    ///  Receives an endorsement request success response.
    /// </summary>
    public class EndorsementSuccessBehavior : NetworkPeerBehavior
    {
        private readonly IEndorsementSuccessHandler requestHandler;

        public EndorsementSuccessBehavior(IEndorsementSuccessHandler requestHandler)
        {
            this.requestHandler = requestHandler;
        }

        protected override void AttachCore()
        {
            // TODO: At the moment every peer is listening for endorsement requests. We may want this to be turned off or on?
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        public override object Clone()
        {
            return new EndorsementSuccessBehavior(this.requestHandler);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is EndorsementPayload payload))
                return;

            await this.requestHandler.ProcessEndorsementAsync(payload.ProposalId, payload.ProposalResponse, peer);
        }
    }
}

