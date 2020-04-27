using System;
using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public class PrivateDataRequestBehavior : NetworkPeerBehavior
    {
        private readonly ITransientStore transientStore;

        public PrivateDataRequestBehavior(ITransientStore transientStore)
        {
            this.transientStore = transientStore;
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        public override object Clone()
        {
            return new PrivateDataRequestBehavior(this.transientStore);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is RequestPrivateDataPayload payload))
                return;

            // TODO: Check they're allowed to access this?

            // See if we have the data.
            (TransientStorePrivateData Data, uint BlockHeight) entry = this.transientStore.Get(payload.Id);

            // If we do, send it to them.
            if (entry.Data != null)
            {
                try
                {
                    await peer.SendMessageAsync(new PrivateDataPayload(payload.Id, entry.BlockHeight, entry.Data.ToBytes())).ConfigureAwait(false);
                }
                catch (OperationCanceledException e)
                {
                    // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
                }
            }
        }
    }
}
