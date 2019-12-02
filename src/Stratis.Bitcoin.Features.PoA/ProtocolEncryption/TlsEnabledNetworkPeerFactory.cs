using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class TlsEnabledNetworkPeerFactory : NetworkPeerFactory
    {
        private readonly CertificatesManager certManager;

        public TlsEnabledNetworkPeerFactory(Network network, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider, ISelfEndpointTracker selfEndpointTracker,
            IInitialBlockDownloadState initialBlockDownloadState, ConnectionManagerSettings connectionManagerSettings, IAsyncProvider asyncProvider, CertificatesManager certManager)
            : base(network, dateTimeProvider, loggerFactory, payloadProvider, selfEndpointTracker, initialBlockDownloadState, connectionManagerSettings, asyncProvider)
        {
            this.certManager = certManager;
        }

        public override INetworkPeerConnection CreateNetworkPeerConnection(INetworkPeer peer, TcpClient client, ProcessMessageAsync<IncomingMessage> processMessageAsync, bool isServer)
        {
            Guard.NotNull(peer, nameof(peer));
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(processMessageAsync, nameof(processMessageAsync));

            int id = Interlocked.Increment(ref this.lastClientId);
            return new TlsEnabledNetworkPeerConnection(this.network, peer, client, id, processMessageAsync, this.dateTimeProvider, this.loggerFactory, this.payloadProvider, this.asyncProvider, this.certManager, isServer);
        }
    }
}
