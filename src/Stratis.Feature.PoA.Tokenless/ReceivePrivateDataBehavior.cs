using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.Features.BlockStore;
using Stratis.SmartContracts.Core.ReadWrite;
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
        private readonly IPrivateDataStore privateDataStore;
        private readonly IBlockRepository blockRepository;

        public ReceivePrivateDataBehavior(
            ITransientStore transientStore,
            IMissingPrivateDataStore missingPrivateDataStore,
            IPrivateDataStore privateDataStore,
            IBlockRepository blockRepository)
        {
            this.transientStore = transientStore;
            this.missingPrivateDataStore = missingPrivateDataStore;
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
            return new ReceivePrivateDataBehavior(this.transientStore, this.missingPrivateDataStore, this.privateDataStore, this.blockRepository);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is PrivateDataPayload payload))
                return;

            if (this.transientStore.Get(payload.TransactionId).Data == null)
            {
                // At the moment we're always storing in the transient store. This is the only way for us to know that we've received the RWS.
                this.transientStore.Persist(payload.TransactionId, payload.BlockHeight, new TransientStorePrivateData(payload.ReadWriteSetData));
            }

            if (this.blockRepository.TransactionExists(payload.TransactionId))
            {
                // The transaction is in a block already - apply the RWS to the private data db.
                ReadWriteSet rws = ReadWriteSet.FromJsonEncodedBytes(payload.ReadWriteSetData);

                // TODO: Validate the read set so that the data is committed in the correct order always.

                foreach (WriteItem write in rws.Writes)
                {
                    this.privateDataStore.StoreBytes(write.ContractAddress, write.Key, write.Value);
                }
            }

            // Also remove this from the missing data store in case it was in there!
            this.missingPrivateDataStore.Remove(payload.TransactionId);
        }
    }
}

