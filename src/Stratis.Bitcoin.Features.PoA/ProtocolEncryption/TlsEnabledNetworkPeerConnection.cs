using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class TlsEnabledNetworkPeerConnection : NetworkPeerConnection
    {
        private readonly CertificatesManager certManager;

        private X509Certificate peerCertificate;

        private readonly bool isServer;

        private CustomTlsServer tlsServer;

        private TlsServerProtocol tlsServerProtocol;

        private CustomTlsClient tlsClient;

        private TlsClientProtocol tlsClientProtocol;

        public TlsEnabledNetworkPeerConnection(Network network, INetworkPeer peer, TcpClient client, int clientId, ProcessMessageAsync<IncomingMessage> processMessageAsync,
            IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider, IAsyncProvider asyncProvider, CertificatesManager certManager, bool isServer)
            : base(network, peer, client, clientId, processMessageAsync, dateTimeProvider, loggerFactory, payloadProvider, asyncProvider)
        {
            this.certManager = certManager;
            this.isServer = isServer;
        }

        public X509Certificate GetPeerCertificate()
        {
            return this.peerCertificate;
        }

        protected override Stream GetStream()
        {
            if (this.stream != null)
                return this.stream;

            this.stream = this.tcpClient.GetStream();

            X509Certificate receivedCert;
            if (this.isServer)
            {
                // We call it a 'client certificate' but for peers connecting to us, we are the server and thus use our client certificate as the server's certificate.
                this.tlsServer = new CustomTlsServer(this.certManager.ClientCertificate, this.certManager.ClientCertificatePrivateKey);
                this.tlsServerProtocol = new TlsServerProtocol(this.stream, new SecureRandom());
                this.tlsServerProtocol.Accept(this.tlsServer);
                receivedCert = this.tlsServer.ReceivedCertificate;
                this.stream = this.tlsServerProtocol.Stream;
            }
            else
            {
                this.tlsClient = new CustomTlsClient(null, this.certManager.ClientCertificate, this.certManager.ClientCertificatePrivateKey);
                this.tlsClientProtocol = new TlsClientProtocol(this.stream, new SecureRandom());
                this.tlsClientProtocol.Connect(this.tlsClient);
                receivedCert = this.tlsClient.Authentication.ReceivedCertificate;
                this.stream = this.tlsClientProtocol.Stream;
            }

            this.peerCertificate = receivedCert;

            if (!CaCertificatesManager.ValidateCertificateChain(this.certManager.AuthorityCertificate, this.peerCertificate))
                return null;

            return this.stream;
        }
    }

    #region Derived versions of the stock Bouncy Castle TLS classes
    public class CustomTlsAuthentication : TlsAuthentication
    {
        private readonly TlsContext mContext;
        private X509Certificate certificate;
        private AsymmetricKeyParameter privateKey;

        public X509Certificate ReceivedCertificate { get; private set; }

        internal CustomTlsAuthentication(TlsContext context, X509Certificate certificate, AsymmetricKeyParameter privateKey)
        {
            this.mContext = context;
            this.certificate = certificate;
            this.privateKey = privateKey;
        }

        public virtual void NotifyServerCertificate(Certificate serverCertificate)
        {
            X509CertificateStructure[] chain = serverCertificate.GetCertificateList();

            if (chain.Length != 1)
                return;

            var certParser = new X509CertificateParser();

            this.ReceivedCertificate = certParser.ReadCertificate(chain[0].GetDerEncoded());
        }

        public virtual TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            byte[] certificateTypes = certificateRequest.CertificateTypes;
            if (certificateTypes == null || !Arrays.Contains(certificateTypes, ClientCertificateType.ecdsa_sign))
                return null;

            var cert = new Certificate(new X509CertificateStructure[] { this.certificate.CertificateStructure });
            var sigAlg = new SignatureAndHashAlgorithm(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa);

            return new DefaultTlsSignerCredentials(this.mContext, cert, privateKey, sigAlg);
        }
    }

    public class CustomTlsClient : DefaultTlsClient
    {
        private TlsSession mSession;
        private X509Certificate certificate;
        private AsymmetricKeyParameter privateKey;

        public CustomTlsAuthentication Authentication { get; private set; }

        public CustomTlsClient(TlsSession session, X509Certificate certificate, AsymmetricKeyParameter privateKey)
        {
            this.mSession = session;
            this.certificate = certificate;
            this.privateKey = privateKey;
        }

        public override TlsSession GetSessionToResume()
        {
            return this.mSession;
        }

        public override void NotifyAlertRaised(byte alertLevel, byte alertDescription, string message, Exception cause)
        {
        }

        public override void NotifyAlertReceived(byte alertLevel, byte alertDescription)
        {
        }

        public override TlsAuthentication GetAuthentication()
        {
            this.Authentication = new CustomTlsAuthentication(mContext, this.certificate, this.privateKey);

            return this.Authentication;
        }

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();

            TlsSession newSession = mContext.ResumableSession;

            if (newSession == null) 
                return;

            byte[] newSessionID = newSession.SessionID;

            if (this.mSession != null && Arrays.AreEqual(this.mSession.SessionID, newSessionID))
            {
                // Resumed session
            }
            else
            {
                // Established session
            }

            this.mSession = newSession;
        }
    }

    public class CustomTlsServer : DefaultTlsServer
    {
        private X509Certificate certificate;
        private AsymmetricKeyParameter privateKey;

        public X509Certificate ReceivedCertificate { get; private set; }

        public CustomTlsServer(X509Certificate certificate, AsymmetricKeyParameter privateKey) : base()
        {
            this.certificate = certificate;
            this.privateKey = privateKey;
        }

        public override void NotifyAlertRaised(byte alertLevel, byte alertDescription, string message, Exception cause)
        {
        }

        public override void NotifyAlertReceived(byte alertLevel, byte alertDescription)
        {
        }

        protected override int[] GetCipherSuites()
        {
            return new int[]
            {
                CipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256
            };
        }

        protected override ProtocolVersion MaximumVersion
        {
            get { return ProtocolVersion.TLSv12; }
        }

        public override ProtocolVersion GetServerVersion()
        {
            ProtocolVersion serverVersion = base.GetServerVersion();

            return serverVersion;
        }

        public override CertificateRequest GetCertificateRequest()
        {
            byte[] certificateTypes = new byte[]{ ClientCertificateType.ecdsa_sign };
            
            // TODO: Do we need to send the actual CA certificate's subject down the wire?
            IList certificateAuthorities = new ArrayList() { this.certificate.IssuerDN };

            return new CertificateRequest(certificateTypes, new[] { new SignatureAndHashAlgorithm(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa) }, certificateAuthorities);
        }

        public override void NotifyClientCertificate(Certificate clientCertificate)
        {
            X509CertificateStructure[] chain = clientCertificate.GetCertificateList();

            if (chain.Length != 1)
                return;

            var certParser = new X509CertificateParser();

            this.ReceivedCertificate = certParser.ReadCertificate(chain[0].GetDerEncoded());
        }

        protected override TlsSignerCredentials GetECDsaSignerCredentials()
        {
            var cert = new Certificate(new X509CertificateStructure[] { this.certificate.CertificateStructure });
            var sigAlg = new SignatureAndHashAlgorithm(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa);
            
            return new DefaultTlsSignerCredentials(this.mContext, cert, this.privateKey, sigAlg);
        }
    }
    #endregion
}
