using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority.Tests.Common;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core;
using Stratis.Core.Controllers.Models;
using Stratis.Core.P2P;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.Api;
using Stratis.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeChannelNetworkTests
    {
        private readonly TokenlessNetwork network;

        public TokenlessNodeChannelNetworkTests() : base()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public void InfraNodeCanCreateAndStartSystemChannelNode()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            Process channelNodeProcess = null;

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var network = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create and start the main "infra" tokenless node which will internally start the "system channel node".
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, server);
                infraNode.Start();

                var channelService = infraNode.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.True(channelService.StartedChannelNodes.Count == 1);

                channelNodeProcess = Process.GetProcessById(channelService.StartedChannelNodes.First().Process.Id);

                Assert.False(channelNodeProcess.HasExited);

                DateTime flagFall = DateTime.Now;

                infraNode.Kill();

                TestBase.WaitLoop(() => { return channelNodeProcess.HasExited; });

                // If this is less than 10 seconds then the system channel node was shutdown gracefully.
                Assert.True((DateTime.Now - flagFall) < TimeSpan.FromMilliseconds(ChannelNodeProcess.MillisecondsBeforeForcedKill));
            }
        }

        [Fact]
        public void CanRestartChannelNodes()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            var processes = new List<Process>();

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Create and start the parent node.
                CoreNode parentNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server);
                parentNode.Start();

                // Create 5 channels for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, "marketing", 2);
                nodeBuilder.CreateChannel(parentNode, "sales", 3);
                nodeBuilder.CreateChannel(parentNode, "legal", 4);
                nodeBuilder.CreateChannel(parentNode, "it", 5);
                nodeBuilder.CreateChannel(parentNode, "humanresources", 6);

                // Re-start the parent node as to load and start the channels it belongs to.
                parentNode.Restart();

                // Ensure that the node started the other daemons, each belonging to their own channel (network).
                var channelService = parentNode.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.True(channelService.StartedChannelNodes.Count == 5);

                foreach (var channel in channelService.StartedChannelNodes)
                {
                    var process = channel.Process;
                    Assert.False(process.HasExited);

                    processes.Add(process);
                }
            }

            foreach (var process in processes)
            {
                Assert.True(process.HasExited);
            }
        }


        [Fact]
        public void CanJoinChannelNode()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Create and start the parent node.
                CoreNode parentNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server);
                parentNode.Start();

                string anotherOrg = "AnotherOrganisation";

                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, "marketing", 2, new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        CaTestHelper.TestOrganisation,
                        anotherOrg
                    }
                });
                parentNode.Restart();

                // Create another node.
                CoreNode otherNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 1, server);
                otherNode.Start();

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(parentNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", "marketing")
                    .GetStringAsync()
                    .GetAwaiter().GetResult();

                // Change the API Port. This is just so our second node on this channel can run without issues
                ChannelNetwork channelNetwork = JsonSerializer.Deserialize<ChannelNetwork>(networkJson);
                channelNetwork.DefaultAPIPort = 60003;
                string updatedNetworkJson = JsonSerializer.Serialize(channelNetwork);

                // Join the channel.
                var response = $"{(new ApiSettings(otherNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = updatedNetworkJson
                    })
                    .GetAwaiter().GetResult();

                var channelService = otherNode.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.Single(channelService.StartedChannelNodes);

                // Start a node from an allowed organisation and ensure it can join too.
                // Create another node.
                CoreNode otherNode2 = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 2, server, organisation: anotherOrg);
                otherNode2.Start();

                // Change the API Port. This is just so our second node on this channel can run without issues
                ChannelNetwork channelNetwork2 = JsonSerializer.Deserialize<ChannelNetwork>(networkJson);
                channelNetwork2.DefaultAPIPort = 65003;
                string updatedNetworkJson2 = JsonSerializer.Serialize(channelNetwork2);

                var otherNode2Response = $"{(new ApiSettings(otherNode2.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = updatedNetworkJson2
                    })
                    .GetAwaiter().GetResult();

                var otherNode2ChannelService = otherNode2.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.Single(otherNode2ChannelService.StartedChannelNodes);

                // Channels are started, now check that all nodes can connect to each other.
                TestHelper.ConnectNoCheck(parentNode, otherNode);
                TestHelper.ConnectNoCheck(parentNode, otherNode2);
                TestHelper.ConnectNoCheck(otherNode, otherNode2);

                Task.Delay(500);

                var addressManagers = new[] {
                    parentNode.FullNode.NodeService<IPeerAddressManager>(),
                    otherNode.FullNode.NodeService<IPeerAddressManager>(),
                    otherNode2.FullNode.NodeService<IPeerAddressManager>()
                };

                Assert.True(addressManagers.All(a => a.Peers.Any()));
            }
        }

        [Fact]
        public void CanNotJoinChannelNode()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Create and start the parent node.
                CoreNode parentNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server);
                parentNode.Start();

                string anotherOrg = "AnotherOrganisation";

                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, "marketing", 2, new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        CaTestHelper.TestOrganisation,
                        anotherOrg
                    }
                });
                parentNode.Restart();

                // Create another node.
                CoreNode otherNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 1, server, organisation: "disallowedOrg");
                otherNode.Start();

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(parentNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", "marketing")
                    .GetStringAsync()
                    .GetAwaiter().GetResult();

                // Change the API Port. This is just so our second node on this channel can run without issues
                ChannelNetwork channelNetwork = JsonSerializer.Deserialize<ChannelNetwork>(networkJson);
                channelNetwork.DefaultAPIPort = 60003;
                string updatedNetworkJson = JsonSerializer.Serialize(channelNetwork);

                // Join the channel.
                var response = $"{(new ApiSettings(otherNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = updatedNetworkJson
                    })
                    .GetAwaiter().GetResult();

                var channelService = otherNode.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.Single(channelService.StartedChannelNodes);

                // Try to connect the nodes
                TestHelper.ConnectNoCheck(parentNode, otherNode);

                var addressManagers = new[] {
                    parentNode.FullNode.NodeService<IPeerAddressManager>(),
                    otherNode.FullNode.NodeService<IPeerAddressManager>(),
                };

                Task.Delay(500);

                // Node should not be allowed to connect because it is disallowed.
                Assert.False(addressManagers.Any(a => a.Peers.Any()));
            }
        }

        [Fact]
        public async Task SystemChannelCreateChannelFromChannelRequestTxAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var network = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create and start the main "infra" tokenless node which will internally start the "system channel node".
                CoreNode infraNode1 = nodeBuilder.CreateInfraNode(network, 0, server);
                infraNode1.Start();

                var channelService = infraNode1.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.True(channelService.StartedChannelNodes.Count == 1);

                // Create second system node.
                CoreNode infraNode2 = nodeBuilder.CreateInfraNode(network, 1, server);
                TokenlessTestHelper.AddCertificatesToMembershipServices(new List<X509Certificate>() { infraNode2.ClientCertificate.ToCertificate() }, infraNode2.DataFolder, network);
                infraNode2.Start();

                // Connect the existing node to it.
                await $"http://localhost:{infraNode1.SystemChannelApiPort}/api"
                    .AppendPathSegment("connectionmanager/addnode")
                    .SetQueryParam("endpoint", $"127.0.0.1:{infraNode2.ProtocolPort}")
                    .SetQueryParam("command", "add").GetAsync();

                // Wait until the first system channel node's tip has advanced beyond bootstrap mode.
                // This proves that the system channel nodes connected.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        var nodeStatus = $"http://localhost:{infraNode1.SystemChannelApiPort}/api".AppendPathSegment("node/status").GetJsonAsync<NodeStatusModel>().GetAwaiter().GetResult();
                        return nodeStatus.ConsensusHeight == 2;
                    }
                    catch (Exception) { }

                    return false;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds, waitTimeSeconds: 120);

                // Call the create channel API method on the system channel node.
                var channelCreationRequest = new ChannelCreationRequest()
                {
                    Name = "Sales",
                    AccessList = new AccessControlList
                    {
                        Organisations = new List<string>
                        {
                            CaTestHelper.TestOrganisation
                        }
                    }
                };

                var response = await $"http://localhost:{infraNode1.SystemChannelApiPort}/api".AppendPathSegment("channels/create").PostJsonAsync(channelCreationRequest);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                // Wait until the transaction has arrived in the first system channel node's mempool.
                TestBase.WaitLoop(() =>
                {
                    var mempoolResponse = $"http://localhost:{infraNode1.SystemChannelApiPort}/api".AppendPathSegment("mempool/getrawmempool").GetJsonAsync<List<string>>().GetAwaiter().GetResult();
                    return mempoolResponse.Count == 1;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds);

                // Wait until the "sales" channel has been created and the node is running.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        dynamic channelNetwork = $"http://localhost:{infraNode1.SystemChannelApiPort}/api"
                            .AppendPathSegment("channels/networkjson")
                            .SetQueryParam("cn", "Sales")
                            .GetJsonAsync()
                            .GetAwaiter().GetResult();

                        var nodeStatus = $"http://localhost:{channelNetwork.defaultapiport}/api".AppendPathSegment("node/status").GetJsonAsync<NodeStatusModel>().GetAwaiter().GetResult();
                        return nodeStatus.State == FullNodeState.Started.ToString();
                    }
                    catch (Exception) { }

                    return false;

                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds);
            }
        }

        [Fact]
        public void DebugInfraNodes()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            Process channelNodeProcess = null;

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var network = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                //CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, server, true);
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, server, true);
                infraNode.Start();

                var channelService = infraNode.FullNode.NodeService<IChannelService>() as InProcessChannelService;
                Assert.True(channelService.ChannelNodes.Count == 1);
            }
        }

        [Fact]
        public void DebugChannelNodes()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                // Create a tokenless node
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                CoreNode node = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server, debugChannels: true);

                node.Start();

                // Create 5 channels for the identity to be apart of.
                nodeBuilder.CreateChannel(node, "marketing", 2);
                nodeBuilder.CreateChannel(node, "sales", 3);
                nodeBuilder.CreateChannel(node, "legal", 4);
                nodeBuilder.CreateChannel(node, "it", 5);
                nodeBuilder.CreateChannel(node, "humanresources", 6);

                // Re-start the parent node as to load and start the channels it belongs to.
                node.Restart();

                Thread.Sleep(10_000);

                var channelService = node.FullNode.NodeService<IChannelService>() as InProcessChannelService;

                Assert.Equal(5, channelService.ChannelNodes.Count);
            }
        }

        [Fact]
        public async Task ExecuteSmartContractOnChannelAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Create both nodes.
                CoreNode node1 = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server, debugChannels:true);
                CoreNode node2 = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 1, server, debugChannels: true);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

                // Start parent node.
                node1.Start();

                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(node1, "marketing", 2);
                node1.Restart();

                // Start second node.
                node2.Start();

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(node1.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", "marketing")
                    .GetStringAsync()
                    .GetAwaiter().GetResult();

                // Join the channel.
                var response = $"{(new ApiSettings(node2.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson
                    })
                    .GetAwaiter().GetResult();

                var channelService1 = node1.FullNode.NodeService<IChannelService>() as InProcessChannelService;
                Assert.Single(channelService1.ChannelNodes);

                var channelService2 = node2.FullNode.NodeService<IChannelService>() as InProcessChannelService;
                Assert.Single(channelService2.ChannelNodes);

                var node1Channel = channelService1.ChannelNodes.First();
                var node2Channel = channelService2.ChannelNodes.First();

                TestHelper.Connect(node1Channel, node2Channel);

                var addressManagers = new[] {
                    node1Channel.FullNode.NodeService<IPeerAddressManager>(),
                    node2Channel.FullNode.NodeService<IPeerAddressManager>(),
                };

                await Task.Delay(500);

                Assert.True(addressManagers.All(a => a.Peers.Any()));

                // Create a contract
                var node1ChannelController = node1Channel.FullNode.NodeController<TokenlessController>();

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");

                var createModel = new BuildCreateContractTransactionModel()
                {
                    ContractCode = compilationResult.Compilation
                };

                var createResult = (JsonResult)node1ChannelController.BuildCreateContractTransaction(createModel);
                var createResponse = (BuildCreateContractTransactionResponse)createResult.Value;

                var txResponse = await node1ChannelController.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = createResponse.Hex
                });

                TestBase.WaitLoop(() => node2Channel.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1Channel.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1Channel, node2Channel);

                Assert.Contains(node1Channel.GetTip().Block.Transactions, t => t.GetHash() == createResponse.TransactionId);

                // Call a contract
                var node2ChannelController = node2Channel.FullNode.NodeController<TokenlessController>();

                var callResult = (JsonResult) node2ChannelController.BuildCallContractTransaction(new BuildCallContractTransactionModel
                {
                    Address = createResponse.NewContractAddress,
                    MethodName = "CallMe"
                });

                var callResponse = (BuildCallContractTransactionResponse) callResult.Value;

                var callTxResponse = await node1ChannelController.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = callResponse.Hex
                });

                TestBase.WaitLoop(() => node1Channel.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1Channel.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1Channel, node2Channel);

                Assert.Contains(node1Channel.GetTip().Block.Transactions, t => t.GetHash() == callResponse.TransactionId);

                var receiptRepo = node1Channel.FullNode.NodeService<IReceiptRepository>();
                Receipt receipt = receiptRepo.Retrieve(callResponse.TransactionId);
                Assert.True(receipt.Success);
            }
        }
    }
}
