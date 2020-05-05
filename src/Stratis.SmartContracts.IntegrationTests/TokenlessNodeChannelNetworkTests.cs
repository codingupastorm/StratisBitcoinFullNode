using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
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
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);
                CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                // Create and start the main "infra" tokenless node which will internally start the "system channel node".
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, ac, client1);
                infraNode.Start();

                var channelService = infraNode.FullNode.NodeService<IChannelService>();
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
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);
                CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                // Create and start the parent node.
                CoreNode parentNode = nodeBuilder.CreateTokenlessNode(tokenlessNetwork, 0, ac, client1, willStartChannels: true);
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
                var channelService = parentNode.FullNode.NodeService<IChannelService>();
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
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create and start the parent node.
                CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);
                CoreNode parentNode = nodeBuilder.CreateTokenlessNode(tokenlessNetwork, 0, ac, client1, willStartChannels: true);
                parentNode.Start();

                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, "marketing", 2);
                parentNode.Restart();

                // Create another node.
                CaClient client2 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);
                CoreNode otherNode = nodeBuilder.CreateTokenlessNode(tokenlessNetwork, 1, ac, client2, willStartChannels: true);
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

                IChannelService channelService = parentNode.FullNode.NodeService<IChannelService>();
                Assert.Single(channelService.StartedChannelNodes);
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
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);
                CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                // Create and start the main "infra" tokenless node which will internally start the "system channel node".
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, ac, client1);
                infraNode.Start();

                var channelService = infraNode.FullNode.NodeService<IChannelService>();
                Assert.True(channelService.StartedChannelNodes.Count == 1);

                var org = infraNode.ClientCertificate.ToCertificate().GetOrganisation();

                // Call the create channel API method on the system channel node.
                var channelCreationRequest = new ChannelCreationRequest()
                {
                    Name = "Sales",
                    Organisation = CaTestHelper.TestOrganisation
                };

                var response = await $"http://localhost:{channelService.GetDefaultAPIPort(ChannelService.SystemChannelId)}/api".AppendPathSegment("channels/create").PostJsonAsync(channelCreationRequest);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                // Wait until the transaction has arrived in the system channel node's mempool.
                TestBase.WaitLoop(() =>
                {
                    var mempoolResponse = $"http://localhost:{channelService.GetDefaultAPIPort(ChannelService.SystemChannelId)}/api".AppendPathSegment("mempool/getrawmempool").GetJsonAsync<List<string>>().GetAwaiter().GetResult();
                    return mempoolResponse.Count == 1;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds);

                // Wait until the block has been mined on the system channel node.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        var nodeStatus = $"http://localhost:{channelService.GetDefaultAPIPort(ChannelService.SystemChannelId)}/api".AppendPathSegment("node/status").GetJsonAsync<NodeStatusModel>().GetAwaiter().GetResult();
                        return nodeStatus.ConsensusHeight == 2;
                    }
                    catch (Exception)
                    {
                    }

                    return false;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds, waitTimeSeconds: 120);

                // Wait until the "sales" channel has been created and the node is running.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        dynamic channelNetwork = $"http://localhost:{channelService.GetDefaultAPIPort(ChannelService.SystemChannelId)}/api"
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
    }
}
