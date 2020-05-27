﻿using System.Collections.Generic;
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
                CoreNode parentNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 0, server, debugChannels: true);
                CoreNode otherNode = nodeBuilder.CreateTokenlessNodeWithChannels(tokenlessNetwork, 1, server, organisation: disallowedOrg, debugChannels: true);

                var certificates = new List<X509Certificate>() { parentNode.ClientCertificate.ToCertificate(), otherNode.ClientCertificate.ToCertificate() };
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, parentNode.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, otherNode.DataFolder, this.network);

                parentNode.Start();
                // Create a channel for the identity to be apart of.
                nodeBuilder.CreateChannel(parentNode, channelName, 2);
                parentNode.Restart();

                // Start other node.
                otherNode.Start();

                TestHelper.Connect(parentNode, otherNode);
                TestHelper.WaitForNodeToSync(parentNode, otherNode);

                // Get the marketing network's JSON.
                string networkJson = $"{(new ApiSettings(parentNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/networkjson")
                    .SetQueryParam("cn", channelName)
                    .GetStringAsync()
                    .GetAwaiter().GetResult();

                // Join the channel.
                var response = $"{(new ApiSettings(otherNode.FullNode.Settings)).ApiUri}"
                    .AppendPathSegment("api/channels/join")
                    .PostJsonAsync(new ChannelJoinRequest()
                    {
                        NetworkJson = networkJson,
                        ApiPort = 60003
                    })
                    .GetAwaiter().GetResult();

                var parentNodeChannelService = otherNode.FullNode.NodeService<IChannelService>() as TestChannelService;
                Assert.Single(parentNodeChannelService.ChannelNodes);

                var otherNodeChannelService = otherNode.FullNode.NodeService<IChannelService>() as TestChannelService;
                
                var otherNodeChannel = otherNodeChannelService.ChannelNodes.First();
                var parentNodeChannel = parentNodeChannelService.ChannelNodes.First();

                // Try to connect the nodes
                // IMPORTANT: Must connect FROM otherNode TO parentNode to ensure the connection is inbound on the parent and the cert check is done.
                TestHelper.ConnectNoCheck(otherNodeChannel, parentNodeChannel);

                await Task.Delay(500);

                // Node should not be allowed to connect because it is disallowed.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(otherNodeChannel, parentNodeChannel));

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
                parentNode.MineBlocksAsync(2).GetAwaiter().GetResult();
                TestHelper.WaitForNodeToSync(parentNode, otherNode);

                // Hacky - need to restart to ensure the allowed orgs list is updated.
                //parentNodeChannel.Restart();

                // Now that the org is allowed try to connect the nodes again
                TestHelper.Connect(otherNodeChannel, parentNodeChannel);

                await Task.Delay(500);

                // Node should be allowed to connect now!
                Assert.True(TestHelper.IsNodeConnectedTo(otherNodeChannel, parentNodeChannel));
            }
        }
    }
}