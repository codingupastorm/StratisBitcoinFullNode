using System;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Core.P2P.Peer;
using Stratis.Core.P2P.Protocol;
using Stratis.Core.P2P.Protocol.Behaviors;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.ProtocolEncryption
{
    public class RevocationBehavior : NetworkPeerBehavior
    {
        private readonly Network network;

        private readonly ILoggerFactory loggerFactory;

        private readonly IMembershipServicesDirectory membershipServices;

        public RevocationBehavior(
            Network network,
            ILoggerFactory loggerFactory,
            IMembershipServicesDirectory membershipServices)
        {
            this.network = network;
            this.loggerFactory = loggerFactory;
            this.membershipServices = membershipServices;
        }

        [NoTrace]
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(peer.Connection is TlsEnabledNetworkPeerConnection connection))
                return;

            // TODO: Check the connection sequence, presumably this gets run after the version handshake (bad). If so, we want to ban/disconnect rogue peers as early as possible.

            X509Certificate rawCert = connection.GetPeerCertificate();

            if (rawCert == null)
            {
                peer.Disconnect("Peer has no certificate.");

                return;
            }

            string certificateP2pkh = MembershipServicesDirectory.ExtractCertificateExtensionString(rawCert, CaCertificatesManager.P2pkhExtensionOid);

            BitcoinAddress address;

            try
            {
                address = BitcoinAddress.Create(certificateP2pkh, this.network);
            }
            catch (Exception)
            {
                address = null;
            }

            if (address == null)
            {
                peer.Disconnect("Peer certificate does not contain a valid address.");

                return;
            }

            if (address.ScriptPubKey.FindTemplate(this.network) != PayToPubkeyHashTemplate.Instance)
            {
                peer.Disconnect("Peer certificate does not contain a P2PKH address.");

                return;
            }

            // TODO: Apart from the existence of the P2PKH address in the certificate, do we need to verify it against anything?
            bool revoked = this.membershipServices.IsCertificateRevoked(MembershipServicesDirectory.GetCertificateThumbprint(rawCert));

            if (revoked)
                peer.Disconnect("Peer certificate is revoked.");
        }

        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        [NoTrace]
        public override object Clone()
        {
            return new RevocationBehavior(this.network, this.loggerFactory, this.membershipServices);
        }
    }
}
