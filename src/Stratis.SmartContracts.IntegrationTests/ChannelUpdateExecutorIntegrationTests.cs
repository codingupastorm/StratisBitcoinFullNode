using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CertificateAuthority.Tests.Common;
using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Hosting;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Controllers.Models;
using Stratis.Core.P2P;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.Api;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ChannelUpdateExecutorIntegrationTests
    {
        private TokenlessNetwork network;

        public ChannelUpdateExecutorIntegrationTests()
        {
            this.network = TokenlessTestHelper.Network;
        }
        
        [Fact]
        public void CanJoinChannelNode()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = this.network;
                var channelName = "marketing";
                string disallowedOrg = "disallowedOrg";

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Create and start the parent node.
                CoreNode parentNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server);
                parentNode.Start();

                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, channelName, 2);
                parentNode.Restart();

                // Create another node.
                CoreNode otherNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 1, server, organisation: disallowedOrg);
                otherNode.Start();

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(parentNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", channelName)
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

                // Send channel update request to add the disallowed org.
                var request = new ChannelUpdateRequest
                {
                    Name = channelName,
                    MembersToRemove = new AccessControlList(),
                    MembersToAdd = new AccessControlList
                    {
                        Organisations = new List<string>
                        {
                            disallowedOrg
                        }
                    }
                };

                // Update the channels
                var updateChannelResponse = $"{(new ApiSettings(parentNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/update")
                    .PostJsonAsync(request)
                    .GetAwaiter().GetResult();

                Assert.Equal(HttpStatusCode.OK, updateChannelResponse.StatusCode);
                TestBase.WaitLoop(() => parentNode.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                parentNode.MineBlocksAsync(1).GetAwaiter().GetResult();

                // Now that the org is allowed try to connect the nodes again
                TestHelper.ConnectNoCheck(parentNode, otherNode);

                Task.Delay(500);

                // Node should be allowed to connect now!
                Assert.True(addressManagers.All(a => a.Peers.Any()));
            }
        }
    }
}