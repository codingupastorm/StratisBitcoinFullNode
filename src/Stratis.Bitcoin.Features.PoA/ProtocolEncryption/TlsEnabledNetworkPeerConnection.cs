using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using NBitcoin;
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

        private readonly bool isServer;

        public TlsEnabledNetworkPeerConnection(Network network, INetworkPeer peer, TcpClient client, int clientId, ProcessMessageAsync<IncomingMessage> processMessageAsync,
            IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider, IAsyncProvider asyncProvider, CertificatesManager certManager, bool isServer)
            : base(network, peer, client, clientId, processMessageAsync, dateTimeProvider, loggerFactory, payloadProvider, asyncProvider)
        {
            this.certManager = certManager;
            this.isServer = isServer;
        }

        protected override Stream GetStream()
        {
            if (this.stream != null)
                return this.stream;

            NetworkStream tcpClientStream = this.tcpClient.GetStream();
            var sslStream = new SslStream(tcpClientStream, false, this.certManager.ValidateCertificate, null);

            try
            {
                if (this.isServer)
                {
                    sslStream.AuthenticateAsServer(this.certManager.ClientCertificate, true, SslProtocols.Tls12, true);
                }
                else
                {
                    var clientCertificateCollection = new X509CertificateCollection(new X509Certificate[] { this.certManager.ClientCertificate });

                    sslStream.AuthenticateAsClient("servName", clientCertificateCollection, SslProtocols.Tls12, false);
                }
            }
            catch (AuthenticationException e)
            {
                this.logger.LogDebug("Exception: {0}", e.Message);

                if (e.InnerException != null)
                    this.logger.LogDebug("Inner exception: {0}", e.InnerException.Message);

                this.logger.LogTrace("(-)[AUTH_FAILED]");
                return this.stream;
            }

            this.stream = sslStream;

            return this.stream;
        }
    }
}
