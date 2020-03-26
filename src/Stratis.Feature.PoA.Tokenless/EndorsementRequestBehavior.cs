using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Payloads;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    ///  Receives endorsement requests and passes them on to be executed etc.
    /// </summary>
    public class EndorsementRequestBehavior : NetworkPeerBehavior
    {
        private readonly IEndorsementRequestHandler requestHandler;

        public EndorsementRequestBehavior(IEndorsementRequestHandler requestHandler)
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
            return new EndorsementRequestBehavior(this.requestHandler);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is ProposalPayload payload))
                return;

            var endorsementRequest = new EndorsementRequest
            {
                ContractTransaction = payload.Transaction,
                Peer = peer,
                TransientData = payload.TransientData
            };

            this.requestHandler.ExecuteAndReturnProposal(endorsementRequest);            
        }
    }
}

