using System;
using System.Threading.Tasks;
using Stratis.Core.P2P.Peer;
using Stratis.Core.P2P.Protocol;
using Stratis.Core.P2P.Protocol.Behaviors;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public class PrivateDataRequestBehavior : NetworkPeerBehavior
    {
        private readonly ITransientStore transientStore;
        private readonly ReadWriteSetPolicyValidator rwsPolicyValidator;

        public PrivateDataRequestBehavior(ITransientStore transientStore, ReadWriteSetPolicyValidator rwsPolicyValidator)
        {
            this.transientStore = transientStore;
            this.rwsPolicyValidator = rwsPolicyValidator;
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
            return new PrivateDataRequestBehavior(this.transientStore, this.rwsPolicyValidator);
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(message.Message.Payload is RequestPrivateDataPayload payload))
                return;

            var cert = ((TlsEnabledNetworkPeerConnection) peer.Connection).GetPeerCertificate();

            // See if we have the data.
            (TransientStorePrivateData Data, uint BlockHeight) entry = this.transientStore.Get(payload.Id);

            // If we do, send it to them.
            if (entry.Data != null)
            {
                ReadWriteSet rws = ReadWriteSet.FromJsonEncodedBytes(entry.Data.ToBytes());

                if (!this.rwsPolicyValidator.OrganisationCanAccessPrivateData(cert, rws))
                {
                    return;
                }

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
