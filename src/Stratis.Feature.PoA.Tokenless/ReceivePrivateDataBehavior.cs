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
        private readonly IMissingPrivateDataStore missingPrivateDataStore;

        public ReceivePrivateDataBehavior(ITransientStore transientStore, IMissingPrivateDataStore missingPrivateDataStore)
        {
            this.transientStore = transientStore;
            this.missingPrivateDataStore = missingPrivateDataStore;
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
            return new ReceivePrivateDataBehavior(this.transientStore, this.missingPrivateDataStore);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is PrivateDataPayload payload))
                return;

            // TODO: Check if we already have the data?

            // TODO: If block is committed, put the data in the private data store.
            this.transientStore.Persist(payload.TransactionId, payload.BlockHeight, new TransientStorePrivateData(payload.ReadWriteSetData));

            // Also remove this from the missing data store in case it was in there!
            this.missingPrivateDataStore.Remove(payload.TransactionId);
        }
    }
}

