using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class RevocationBehavior : NetworkPeerBehavior
    {
        private readonly NodeSettings NodeSettings;

        private readonly Network Network;

        private readonly ILoggerFactory LoggerFactory;

        private readonly ILogger Logger;

        private readonly RevocationChecker RevocationChecker;

        public RevocationBehavior(NodeSettings nodeSettings,
            Network network,
            ILoggerFactory loggerFactory,
            RevocationChecker revocationChecker)
        {
            this.NodeSettings = nodeSettings;
            this.Network = network;
            this.LoggerFactory = loggerFactory;
            this.Logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.RevocationChecker = revocationChecker;
        }

        [NoTrace]
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (!(peer.Connection is TlsEnabledNetworkPeerConnection connection))
                return;

            X509Certificate peerCertificate = connection.GetPeerCertificate();

            if (peerCertificate == null)
            {
                peer.Disconnect("Peer has no certificate.");

                return;
            }

            byte[] certificateP2pkhExtension = CertificatesManager.ExtractCertificateExtension(peerCertificate, "1.4.1") ?? new byte[0];

            string certificateP2pkh = Encoding.UTF8.GetString(certificateP2pkhExtension);

            BitcoinAddress address;

            try
            {
                address = BitcoinAddress.Create(certificateP2pkh, this.Network);
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

            if (address.ScriptPubKey.FindTemplate(this.Network) != PayToPubkeyHashTemplate.Instance)
            {
                peer.Disconnect("Peer certificate does not contain a P2PKH address.");

                return;
            }

            bool revoked = await this.RevocationChecker.IsCertificateRevokedAsync(certificateP2pkh, true);

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
            return new RevocationBehavior(this.NodeSettings, this.Network, this.LoggerFactory, this.RevocationChecker);
        }
    }
}
