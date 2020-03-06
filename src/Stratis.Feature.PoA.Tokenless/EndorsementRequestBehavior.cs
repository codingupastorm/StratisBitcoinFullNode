using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    ///  Receives endorsement requests and passes them on to be executed etc.
    /// </summary>
    public class EndorsementRequestBehavior : NetworkPeerBehavior
    {
        private readonly IEndorsementRequestHandler requestHandler;
        private readonly ITokenlessBroadcaster tokenlessBroadcaster;
        private readonly ITokenlessTransactionFromRWS tokenlessTransactionFromRWS;

        public EndorsementRequestBehavior(IEndorsementRequestHandler requestHandler, ITokenlessBroadcaster tokenlessBroadcaster,
            ITokenlessTransactionFromRWS tokenlessTransactionFromRWS)
        {
            this.requestHandler = requestHandler;
            this.tokenlessBroadcaster = tokenlessBroadcaster;
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
            return new EndorsementRequestBehavior(this.requestHandler, this.tokenlessBroadcaster, this.tokenlessTransactionFromRWS);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is EndorsementRequestPayload payload))
                return;

            var endorsementRequest = new EndorsementRequest
            {
                ContractTransaction = payload.Transaction
            };

            IContractExecutionResult result = this.requestHandler.ExecuteAndSignProposal(endorsementRequest);
            if (result != null)
            {
                Transaction signedRWSTransaction = this.tokenlessTransactionFromRWS.Build(result.ReadWriteSet.GetReadWriteSet());

                // Send the result back.
                await this.tokenlessBroadcaster.BroadcastToFirstInOrganisationAsync(
                    new EndorsementSuccessPayload(signedRWSTransaction),
                    null /* TODO: Pass the organization here. */
                    );
            }
        }
    }
}

