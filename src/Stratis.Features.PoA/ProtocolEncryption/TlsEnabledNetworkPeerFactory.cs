using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.PoA.ProtocolEncryption
{
    public class TlsEnabledNetworkPeerFactory : NetworkPeerFactory
    {
        private readonly ICertificatesManager certManager;
        private readonly NodeSettings nodeSettings;

        public TlsEnabledNetworkPeerFactory(Network network, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider, ISelfEndpointTracker selfEndpointTracker,
            IInitialBlockDownloadState initialBlockDownloadState, ConnectionManagerSettings connectionManagerSettings, IAsyncProvider asyncProvider, ICertificatesManager certManager, IPeerAddressManager peerAddressManager, NodeSettings nodeSettings)
            : base(network, dateTimeProvider, loggerFactory, payloadProvider, selfEndpointTracker, initialBlockDownloadState, connectionManagerSettings, asyncProvider, peerAddressManager)
        {
            this.certManager = certManager;
            this.nodeSettings = nodeSettings;
        }

        public override INetworkPeerConnection CreateNetworkPeerConnection(INetworkPeer peer, TcpClient client, ProcessMessageAsync<IncomingMessage> processMessageAsync, bool isServer)
        {
            Guard.NotNull(peer, nameof(peer));
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(processMessageAsync, nameof(processMessageAsync));

            int id = Interlocked.Increment(ref this.lastClientId);
            return new TlsEnabledNetworkPeerConnection(this.network, peer, client, id, processMessageAsync, this.dateTimeProvider, this.loggerFactory, this.payloadProvider, this.asyncProvider, this.certManager, isServer, nodeSettings);
        }
    }
}
