using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Core.Connection;
using Stratis.Core.Consensus;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Utilities.Extensions;
using Xunit;
using Stratis.Feature.PoA.Tokenless.Networks;
using Microsoft.AspNetCore.Hosting;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using CertificateAuthority;

namespace Stratis.Bitcoin.IntegrationTests.Connectivity
{
    public class ConnectivityTests
    {
        private readonly Network posNetwork;
        private readonly Network powNetwork;

        public ConnectivityTests()
        {
            this.posNetwork = new StratisRegTest();
            this.powNetwork = new BitcoinRegTest();
        }

        [Fact]
        public void Ensure_Node_DoesNot_ReconnectTo_SameNode()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode nodeA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-1-nodeA").Start();
                CoreNode nodeB = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-1-nodeB").Start();

                TestHelper.Connect(nodeA, nodeB);
                TestHelper.ConnectNoCheck(nodeA, nodeB);

                TestBase.WaitLoop(() => nodeA.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);
                TestBase.WaitLoop(() => nodeB.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);

                Assert.False(nodeA.FullNode.ConnectionManager.ConnectedPeers.First().Inbound);
                Assert.True(nodeB.FullNode.ConnectionManager.ConnectedPeers.First().Inbound);
            }
        }

        /// <summary>
        /// Peer A_1 connects to Peer A_2
        /// Peer B_1 connects to Peer B_2
        /// Peer A_1 connects to Peer B_1
        ///
        /// Peer A_1 asks Peer B_1 for its addresses and gets Peer B_2
        /// Peer A_1 now also connects to Peer B_2
        /// </summary>
        [Fact]
        public void Ensure_Peer_CanDiscover_Address_From_ConnectedPeers_And_Connect_ToThem()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode nodeGroupA_1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-2-nodeGroupA_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_1 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-2-nodeGroupB_1").EnablePeerDiscovery().Start();
                CoreNode nodeGroupB_2 = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "conn-2-nodeGroupB_2").EnablePeerDiscovery().Start();

                // Connect B_1 to B_2.
                nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeGroupB_2.Endpoint, IPAddress.Loopback);
                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupB_1, nodeGroupB_2));
                TestBase.WaitLoop(() =>
                {
                    return nodeGroupB_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_2.Endpoint));
                });

                // Connect group A_1 to B_1
                // A_1 will receive B_1's addresses which includes B_2.
                TestHelper.Connect(nodeGroupA_1, nodeGroupB_1);

                //Wait until A_1 contains both B_1 and B_2's addresses in its address manager.
                TestBase.WaitLoop(() =>
                 {
                     var result = nodeGroupA_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_1.Endpoint));
                     if (result)
                         return nodeGroupA_1.FullNode.NodeService<IPeerAddressManager>().Peers.Any(p => p.Endpoint.Match(nodeGroupB_2.Endpoint));
                     return false;
                 });

                // Wait until A_1 connected to B_2.
                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(nodeGroupA_1, nodeGroupB_2));
            }
        }

        [Fact]
        public void When_Connecting_WithAddnode_Connect_ToPeer_AndAnyPeers_InTheAddressManager()
        {
            // TS101_Connectivity_CallAddNode.

            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-3-node1").Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-3-node2").Start();
                CoreNode node3 = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "conn-3-node3").Start();
                CoreNode syncerNode = nodeBuilder.CreateTokenlessNode(network, 3, server, agent: "conn-3-syncerNode").Start();

                TestHelper.Connect(node1, syncerNode);

                node2.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);
                node3.FullNode.ConnectionManager.ConnectedPeers.Count().Should().Be(0);

                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node2.Endpoint);
                syncerNode.FullNode.NodeService<IConnectionManager>().AddNodeAddress(node3.Endpoint);

                TestBase.WaitLoop(() => syncerNode.FullNode.ConnectionManager.ConnectedPeers.Count() == 3);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node2.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
                node3.FullNode.ConnectionManager.ConnectedPeers.Should().ContainSingle();
            }
        }

        [Fact]
        public void When_Connecting_WithConnectOnly_Connect_ToTheRequestedPeer()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-4-node1").Start();

                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", node1.Endpoint.ToString() }
                };

                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-4-node2", configParameters: nodeConfig).Start();

                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(node1, node2));
            }
        }

        [Fact]
        public void BannedNode_Tries_ToConnect_ItFails_ToEstablishConnection()
        {
            // TS105_Connectivity_PreventConnectingToBannedNodes.

            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-5-node1").Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-5-node2").Start();

                node1 = BanNode(node1, node2);

                Action connectAction = () => node1.FullNode.ConnectionManager.ConnectAsync(node2.Endpoint).GetAwaiter().GetResult();
                connectAction.Should().Throw<OperationCanceledException>().WithMessage("The peer has been disconnected");

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();

                node1 = RemoveBan(node1, node2);

                TestHelper.Connect(node1, node2);

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void Not_Fail_IfTry_ToConnect_ToNonExisting_NodeAsync()
        {
            // TS106_Connectivity_CanErrorHandleConnectionToNonExistingNodes.

            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-6-node1").Start();

                var node1ConnectionMgr = node1.FullNode.NodeService<IConnectionManager>();

                var node1PeerNodeConnector = node1ConnectionMgr.PeerConnectors.Where(p => p.GetType() == typeof(PeerConnectorConnectNode)).First() as PeerConnectorConnectNode;

                var nonExistentEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 90);
                node1PeerNodeConnector.ConnectionSettings.Connect = new List<IPEndPoint>() { nonExistentEndpoint };

                node1ConnectionMgr.Initialize(node1.FullNode.NodeService<IConsensusManager>());

                node1PeerNodeConnector.OnConnectAsync().GetAwaiter().GetResult();

                node1.FullNode.ConnectionManager.ConnectedPeers.Should().BeEmpty();
            }
        }

        [Fact]
        public void NodeServer_Disabled_When_ConnectNode_Args_Specified()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", "0" }
                };

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-7-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-7-node2").Start();

                Assert.False(node1.FullNode.ConnectionManager.Servers.Any());

                try
                {
                    // Manually call AddNode so that we can catch the exception.
                    node2.FullNode.ConnectionManager.ConnectAsync(node1.Endpoint).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ex.Message.Contains("actively refused");
                }

                Assert.False(TestHelper.IsNodeConnectedTo(node2, node1));
            }
        }

        [Fact]
        public void NodeServer_Disabled_When_Listen_Specified_AsFalse()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                var nodeConfig = new NodeConfigParameters
                {
                    { "-listen", "0" }
                };

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-8-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-8-node2").Start();
                
                Assert.False(node1.FullNode.ConnectionManager.Servers.Any());

                try
                {
                    // Manually call AddNode so that we can catch the exception.
                    node2.FullNode.ConnectionManager.ConnectAsync(node1.Endpoint).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ex.Message.Contains("actively refused");
                }

                Assert.False(TestHelper.IsNodeConnectedTo(node2, node1));
            }
        }

        [Fact]
        public void NodeServer_Enabled_When_ConnectNode_Args_Specified_And_Listen_Specified()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                var nodeConfig = new NodeConfigParameters
                {
                    { "-connect", "0" },
                    { "-listen", "1" }
                };

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-9-node1", configParameters: nodeConfig).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-9-node2").Start();

                Assert.True(node1.FullNode.ConnectionManager.Servers.Any());

                TestHelper.Connect(node1, node2);

                Assert.True(TestHelper.IsNodeConnectedTo(node2, node1));

                TestHelper.DisconnectAll(node1, node2);
            }
        }

        [Fact]
        public void Node_Gets_Banned_Subsequent_Connections_DoesNot_Affect_InboundCount()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "conn-10-node1").Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "conn-10-node2").Start();

                TestHelper.Connect(node1, node2);

                // node1 bans node2
                var service = node1.FullNode.NodeService<IPeerBanning>();
                service.BanAndDisconnectPeer(node2.Endpoint);

                // Ensure the node is disconnected.
                TestBase.WaitLoop(() => TestHelper.IsNodeConnectedTo(node1, node2) == false);

                // Try and connect to the node that banned me 10 times.
                for (int i = 0; i < 10; i++)
                {
                    TestHelper.ConnectNoCheck(node2, node1);
                    Task.Delay(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
                }

                // Inbound peer count should still be 0.
                var server2 = node1.FullNode.ConnectionManager.Servers.First();
                Assert.True(server2.ConnectedInboundPeersCount == 0);
            }
        }

        private CoreNode BanNode(CoreNode sourceNode, CoreNode nodeToBan)
        {
            sourceNode.FullNode.NodeService<IPeerAddressManager>().AddPeer(nodeToBan.Endpoint, IPAddress.Loopback);

            PeerAddress peerAddress = sourceNode.FullNode.NodeService<IPeerAddressManager>().Peers.FirstOrDefault();

            if (peerAddress != null)
                peerAddress.BanUntil = DateTime.UtcNow.AddMinutes(1);

            return sourceNode;
        }

        private CoreNode RemoveBan(CoreNode sourceNode, CoreNode bannedNode)
        {
            sourceNode.FullNode.NodeService<IPeerAddressManager>().AddPeer(bannedNode.Endpoint, IPAddress.Loopback);

            PeerAddress peerAddress = sourceNode.FullNode.NodeService<IPeerAddressManager>().Peers.FirstOrDefault();

            if (peerAddress != null)
                peerAddress.BanUntil = null;

            return sourceNode;
        }
    }
}
