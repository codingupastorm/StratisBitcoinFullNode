using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority.Tests.Common;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core;
using Stratis.Core.Controllers.Models;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.Api;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeChannelNetworkTests
    {
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

                // Join the channel.
                var response = $"{(new ApiSettings(otherNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson
                    })
                    .GetAwaiter().GetResult();

                var channelService = otherNode.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.Single(channelService.StartedChannelNodes);

                // Start a node from an allowed organisation and ensure it can join too.
                // Create another node.
                CoreNode otherNode2 = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 2, server, organisation: anotherOrg);
                otherNode2.Start();

                var otherNode2Response = $"{(new ApiSettings(otherNode2.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson
                    })
                    .GetAwaiter().GetResult();

                var otherNode2ChannelService = otherNode2.FullNode.NodeService<IChannelService>() as ProcessChannelService;
                Assert.Single(otherNode2ChannelService.StartedChannelNodes);
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

                var channelService = node.FullNode.NodeService<IChannelService>() as TestChannelService;

                Assert.Equal(5, channelService.ChannelNodes.Count);
            }
        }
    }
}
