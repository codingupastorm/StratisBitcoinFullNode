using System.Net.Sockets;
using System.Threading;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Core.AsyncWork;
using Stratis.Features.PoA;

namespace Stratis.Feature.PoA.Tokenless.ProtocolEncryption
{
    public class TlsEnabledNetworkPeerFactory : NetworkPeerFactory
    {
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly IClientCertificateValidator clientCertificateValidator;

        public TlsEnabledNetworkPeerFactory(Network network, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, PayloadProvider payloadProvider, ISelfEndpointTracker selfEndpointTracker,
            IInitialBlockDownloadState initialBlockDownloadState, ConnectionManagerSettings connectionManagerSettings, IAsyncProvider asyncProvider, IPeerAddressManager peerAddressManager, IMembershipServicesDirectory membershipServices, IClientCertificateValidator clientCertificateValidator = null)
            : base(network, dateTimeProvider, loggerFactory, payloadProvider, selfEndpointTracker, initialBlockDownloadState, connectionManagerSettings, asyncProvider, peerAddressManager)
        {
            this.membershipServices = membershipServices;
            this.clientCertificateValidator = clientCertificateValidator;
        }

        public override INetworkPeerConnection CreateNetworkPeerConnection(INetworkPeer peer, TcpClient client, ProcessMessageAsync<IncomingMessage> processMessageAsync, bool isServer)
        {
            Guard.NotNull(peer, nameof(peer));
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(processMessageAsync, nameof(processMessageAsync));
        
            int id = Interlocked.Increment(ref this.lastClientId);
            return new TlsEnabledNetworkPeerConnection(this.network, peer, client, id, processMessageAsync, this.dateTimeProvider, this.loggerFactory, this.payloadProvider, this.asyncProvider, this.membershipServices, isServer, this.clientCertificateValidator);
        }
    }
}
