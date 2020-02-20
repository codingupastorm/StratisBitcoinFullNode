﻿using System;
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
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

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

            // TODO: Check the connection sequence, presumably this gets run after the version handshake (bad). If so, we want to ban/disconnect rogue peers as early as possible.

            X509Certificate rawCert = connection.GetPeerCertificate();

            if (rawCert == null)
            {
                peer.Disconnect("Peer has no certificate.");

                return;
            }

            var peerCertificate = new X509Certificate2(rawCert.GetEncoded());

            string certificateP2pkh = CertificatesManager.ExtractCertificateExtensionString(rawCert, "1.4.1");

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

            // TODO: Apart from the existence of the P2PKH address in the certificate, do we need to verify it against anything?

            bool revoked = await this.RevocationChecker.IsCertificateRevokedAsync(peerCertificate.Thumbprint, true);

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