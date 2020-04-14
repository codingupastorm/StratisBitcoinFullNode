using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    ///  Receives private data when endorsers have successfully executed private data transactions.
    /// </summary>
    public class ReceivePrivateDataBehavior : NetworkPeerBehavior
    {
        private readonly ITransientStore transientStore;

        public ReceivePrivateDataBehavior(ITransientStore transientStore)
        {
            this.transientStore = transientStore;
        }

        protected override void AttachCore()
        {
            // TODO: Only listen 
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        public override object Clone()
        {
            return new ReceivePrivateDataBehavior(this.transientStore);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is PrivateDataPayload payload))
                return;

            // TODO: If block is committed, put the data in the private data store.
            this.transientStore.Persist(payload.TransactionId, payload.BlockHeight, new TransientStorePrivateData(payload.ReadWriteSetData));
        }
    }
}

