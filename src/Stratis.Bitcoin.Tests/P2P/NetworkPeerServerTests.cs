using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Moq;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Configuration.Logging;
using Stratis.Core.Configuration.Settings;
using Stratis.Core.Connection;
using Stratis.Core.Interfaces;
using Stratis.Core.Networks;
using Stratis.Core.P2P;
using Stratis.Core.P2P.Peer;
using Stratis.Core.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class NetworkPeerServerTests : LogsTestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        private readonly ITestOutputHelper testOutput;

        public NetworkPeerServerTests(ITestOutputHelper output)
        {
            this.testOutput = output;
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void Validate_AllowClientConnection_State(bool inIBD, bool isWhiteListed, bool closeClient)
        {
            // Arrange
            var networkPeerFactory = new Mock<INetworkPeerFactory>();
            networkPeerFactory.Setup(npf => npf.CreateConnectedNetworkPeerAsync(It.IsAny<IPEndPoint>(),
                It.IsAny<NetworkPeerConnectionParameters>(),
                It.IsAny<NetworkPeerDisposer>())).Returns(Task.FromResult(new Mock<INetworkPeer>().Object));

            var initialBlockDownloadState = new Mock<IInitialBlockDownloadState>();
            initialBlockDownloadState.Setup(i => i.IsInitialBlockDownload()).Returns(inIBD);

            var nodeSettings = new NodeSettings(new BitcoinRegTest());
            var connectionManagerSettings = new ConnectionManagerSettings(nodeSettings);

            var endpointAddNode = new IPEndPoint(IPAddress.Parse("::ffff:192.168.0.1"), 80);

            var asyncProvider = this.CreateAsyncProvider();

            var peerAddressManager = new Mock<IPeerAddressManager>();
            peerAddressManager.Setup(pam => pam.FindPeersByIp(It.IsAny<IPEndPoint>())).Returns(new List<PeerAddress>());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(a => a.ConnectedPeers.FindByIp(It.IsAny<IPAddress>()))
                .Returns(new List<INetworkPeer>() { });

            var selfEndpointTracker = new Mock<ISelfEndpointTracker>();
            selfEndpointTracker.Setup(a => a.IsSelf(It.IsAny<IPEndPoint>()))
                .Returns(false);

            var networkPeerServer = new NetworkPeerServer(this.Network,
                endpointAddNode,
                endpointAddNode,
                ProtocolVersion.PROTOCOL_VERSION,
                this.extendedLoggerFactory,
                networkPeerFactory.Object,
                initialBlockDownloadState.Object,
                connectionManagerSettings,
                asyncProvider,
                peerAddressManager.Object,
                connectionManager.Object,
                selfEndpointTracker.Object,
                DateTimeProvider.Default);

            // Mimic external client
            const int portNumber = 80;
            var client = new TcpClient("www.stratisplatform.com", portNumber);

            string ip = string.Empty;
            var ipandport = client.Client.RemoteEndPoint.ToString();
            if (client.Client.RemoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
            {
                ip = ipandport.Replace(ipandport.Substring(ipandport.IndexOf(':')), "");
            }
            else
            {
                ip = ipandport.Substring(1, ipandport.LastIndexOf(']') - 1);
            }

            var endpointDiscovered = new IPEndPoint(IPAddress.Parse(ip), portNumber);

            // Include the external client as a NodeServerEndpoint.
            connectionManagerSettings.Bind.Add(new NodeServerEndpoint(endpointDiscovered, isWhiteListed));

            // Act
            var result = networkPeerServer.InvokeMethod("AllowClientConnection", client);

            // Assert
            Assert.True((inIBD && !isWhiteListed) == closeClient);

            this.testOutput.WriteLine(
                $"In IBD : {inIBD.ToString()}, " +
                $"Is White Listed : {isWhiteListed.ToString()}, " +
                $"Close Client : {result.ToString()}");
        }
    }
}