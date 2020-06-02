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
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.P2P;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
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
        public async Task CanJoinChannelNode()
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
                CoreNode infraNode = nodeBuilder.CreateInfraNode(tokenlessNetwork, 0, server, debugChannels: true);
                CoreNode channelNodeParent = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 2, server, debugChannels: true);
                CoreNode disallowedNodeParent = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 3, server, organisation: disallowedOrg, debugChannels: true);

                var certificates = new List<X509Certificate>() { infraNode.ClientCertificate.ToCertificate(), disallowedNodeParent.ClientCertificate.ToCertificate(), channelNodeParent.ClientCertificate.ToCertificate() };
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, infraNode.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, disallowedNodeParent.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, channelNodeParent.DataFolder, this.network);

                infraNode.Start();

                // Create the "marketing" channel.
                var channelDef = nodeBuilder.CreateChannel(infraNode, channelName, 3);

                // Start other nodes.
                channelNodeParent.Start();
                disallowedNodeParent.Start();

                TestHelper.Connect(infraNode, disallowedNodeParent);
                TestHelper.Connect(infraNode, channelNodeParent);
                TestHelper.WaitForNodeToSync(infraNode, disallowedNodeParent);
                TestHelper.WaitForNodeToSync(infraNode, channelNodeParent);

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(infraNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", channelName)
                    .GetStringAsync()
                    .GetAwaiter().GetResult();

                // Attempt to join the channel as the other node.
                var response1 = $"{(new ApiSettings(channelNodeParent.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson,
                        ApiPort = 60002
                    })
                    .GetAwaiter().GetResult();

                var allowedNodeParentChannelService = channelNodeParent.FullNode.NodeService<IChannelService>() as TestChannelService;
                Assert.Single(allowedNodeParentChannelService.ChannelNodes);


                // Attempt to join the channel as the disallowed node.
                var response = $"{(new ApiSettings(disallowedNodeParent.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson,
                        ApiPort = 60003
                    })
                    .GetAwaiter().GetResult();

                var infraNodeChannelService = infraNode.FullNode.NodeService<IChannelService>() as TestChannelService;
                Assert.Single(infraNodeChannelService.ChannelNodes);

                var disallowedNodeParentChannelService = disallowedNodeParent.FullNode.NodeService<IChannelService>() as TestChannelService;

                var allowedNodeChannel = allowedNodeParentChannelService.ChannelNodes.First();
                var disallowedNodeChannel = disallowedNodeParentChannelService.ChannelNodes.First();
                var systemChannel = infraNodeChannelService.ChannelNodes.First();

                // Save the channel def on the system channel.
                IChannelRepository channelRepository = systemChannel.FullNode.NodeService<IChannelRepository>();
                channelRepository.SaveChannelDefinition(channelDef);

                // Try to connect the nodes
                // IMPORTANT: Must connect FROM otherNode TO parentNode to ensure the connection is inbound on the parent and the cert check is done.
                TestHelper.ConnectNoCheck(disallowedNodeChannel, allowedNodeChannel);

                await Task.Delay(500);

                // Node should not be allowed to connect because it is disallowed.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(disallowedNodeChannel, allowedNodeChannel));

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
                var updateChannelResponse = $"{(new ApiSettings(systemChannel.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/update")
                    .PostJsonAsync(request)
                    .GetAwaiter().GetResult();

                Assert.Equal(HttpStatusCode.OK, updateChannelResponse.StatusCode);
                TestBase.WaitLoop(() => systemChannel.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                systemChannel.MineBlocksAsync(2).GetAwaiter().GetResult();
                //TestHelper.WaitForNodeToSync(parentNode, otherNode);

                // Now that the org is allowed try to connect the nodes again
                TestHelper.Connect(disallowedNodeChannel, allowedNodeChannel);

                await Task.Delay(500);

                // Node should be allowed to connect now!
                Assert.True(TestHelper.IsNodeConnectedTo(disallowedNodeChannel, allowedNodeChannel));
            }
        }
    }
}