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
    ///  Receives endorsement requests and passes them on to be executed etc.
    /// </summary>
    public class EndorsementSuccessBehavior : NetworkPeerBehavior
    {
        private readonly IEndorsementRequestHandler requestHandler;
        private readonly ITokenlessTransactionFromRWS tokenlessTransactionFromRWS;

        public EndorsementSuccessBehavior(IEndorsementRequestHandler requestHandler, ITokenlessTransactionFromRWS tokenlessTransactionFromRWS)
        {
            this.requestHandler = requestHandler;
            this.tokenlessTransactionFromRWS = tokenlessTransactionFromRWS;
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
            return new EndorsementSuccessBehavior(this.requestHandler, this.tokenlessTransactionFromRWS);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is EndorsementSuccessPayload payload))
                return;

            Transaction signedRWSTransaction = payload.Transaction;

            ReadWriteSet readWriteSet = this.tokenlessTransactionFromRWS.GetReadWriteSet(signedRWSTransaction);

            // TODO: Act on signed proposal.
        }
    }
}

