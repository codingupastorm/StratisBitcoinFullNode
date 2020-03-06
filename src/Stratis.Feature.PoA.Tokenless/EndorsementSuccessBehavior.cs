using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    ///  Receives an endorsement request success response.
    /// </summary>
    public class EndorsementSuccessBehavior : NetworkPeerBehavior
    {
        private readonly IEndorsementRequestHandler requestHandler;
        private readonly IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer;

        public EndorsementSuccessBehavior(IEndorsementRequestHandler requestHandler, IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer)
        {
            this.requestHandler = requestHandler;
            this.readWriteSetTransactionSerializer = readWriteSetTransactionSerializer;
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
            return new EndorsementSuccessBehavior(this.requestHandler, this.readWriteSetTransactionSerializer);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is EndorsementSuccessPayload payload))
                return;

            Transaction signedRWSTransaction = payload.Transaction;

            ReadWriteSet readWriteSet = this.readWriteSetTransactionSerializer.GetReadWriteSet(signedRWSTransaction);

            // TODO: Act on signed proposal.
        }
    }
}

