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
        private readonly IPrivateDataStore privateDataStore;

        public ReceivePrivateDataBehavior(
            ITransientStore transientStore,
            IPrivateDataStore privateDataStore)
        {
            this.transientStore = transientStore;
            this.privateDataStore = privateDataStore;
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
            return new ReceivePrivateDataBehavior(this.transientStore, this.privateDataStore);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is PrivateDataPayload payload))
                return;

            if (this.transientStore.Get(payload.Id).Data == null)
            {
                // At the moment we're always storing in the transient store. This is the only way for us to know that we've received the RWS.
                this.transientStore.Persist(payload.Id, payload.BlockHeight, new TransientStorePrivateData(payload.ReadWriteSetData));
            }
        }
    }
}

