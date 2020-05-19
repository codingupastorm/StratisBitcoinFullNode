using System;
using System.Collections.Generic;
using System.Net;
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
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ChannelUpdateExecutorIntegrationTests : CaTester
    {
        private TokenlessNetwork network;

        public ChannelUpdateExecutorIntegrationTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public async Task Add_New_Organisation()
        {
            // Add an organisation to a channel
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder, this.caBaseAddress).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(this.caBaseAddress);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Setup network.
                CoreNode infraNode = nodeBuilder.CreateInfraNode(this.network, 0, server, caBaseAddress: this.caBaseAddress);
                infraNode.Start();

                var infraChannelService = infraNode.FullNode.NodeService<IChannelService>();
                Assert.True(infraChannelService.StartedChannelNodes.Count == 1);

                // Create second system node.
                CoreNode infraNode2 = nodeBuilder.CreateInfraNode(network, 1, server, caBaseAddress: this.caBaseAddress);

                //CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 2, server, caBaseAddress: this.caBaseAddress, organisation: "Org1");
                //CoreNode node3 = nodeBuilder.CreateTokenlessNode(this.network, 3, server, caBaseAddress: this.caBaseAddress, organisation: "Org2");

                var certificates = new List<X509Certificate>() { infraNode.ClientCertificate.ToCertificate(), infraNode2.ClientCertificate.ToCertificate(), };//node2.ClientCertificate.ToCertificate(), node3.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, infraNode.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, infraNode2.DataFolder, network);
                //TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);
                //TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node3.DataFolder, this.network);

                infraNode2.Start();
                //node2.Start();
                //node3.Start();

                // Connect the existing node to it.
                await $"http://localhost:{infraNode.SystemChannelApiPort}/api"
                    .AppendPathSegment("connectionmanager/addnode")
                    .SetQueryParam("endpoint", $"127.0.0.1:{infraNode2.ProtocolPort}")
                    .SetQueryParam("command", "add").GetAsync();

                //TestHelper.Connect(infraNode, node2);
                //TestHelper.Connect(node2, node3);
                //TestHelper.Connect(infraNode, node3);

                // Wait until the first system channel node's tip has advanced beyond bootstrap mode.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        var nodeStatus = $"http://localhost:{infraNode.SystemChannelApiPort}/api".AppendPathSegment("node/status").GetJsonAsync<NodeStatusModel>().GetAwaiter().GetResult();
                        return nodeStatus.ConsensusHeight == 2;
                    }
                    catch (Exception) { }

                    return false;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds, waitTimeSeconds: 120);
                var newChannelName = "Sales";

                // Create a new channel.
                var channelCreationRequest = new ChannelCreationRequest()
                {
                    Name = newChannelName,
                    AccessList = new AccessControlList
                    {
                        Organisations = new List<string>
                        {
                            CaTestHelper.TestOrganisation
                        }
                    }
                };

                try
                {
                    var response = await $"http://localhost:{infraNode.SystemChannelApiPort}/api"
                        .AppendPathSegment("channels/create").PostJsonAsync(channelCreationRequest);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                }
                catch (Exception e)
                {
                    return;
                }

                // Wait until the transaction has arrived in the first system channel node's mempool.
                TestBase.WaitLoop(() =>
                {
                    var mempoolResponse = $"http://localhost:{infraNode.SystemChannelApiPort}/api".AppendPathSegment("mempool/getrawmempool").GetJsonAsync<List<string>>().GetAwaiter().GetResult();
                    return mempoolResponse.Count == 1;
                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds);

                // Wait until the "sales" channel has been created and the node is running.
                TestBase.WaitLoop(() =>
                {
                    try
                    {
                        dynamic channelNetwork = $"http://localhost:{infraNode.SystemChannelApiPort}/api"
                            .AppendPathSegment("channels/networkjson")
                            .SetQueryParam("cn", newChannelName)
                            .GetJsonAsync()
                            .GetAwaiter().GetResult();

                        var nodeStatus = $"http://localhost:{channelNetwork.defaultapiport}/api".AppendPathSegment("node/status").GetJsonAsync<NodeStatusModel>().GetAwaiter().GetResult();
                        return nodeStatus.State == FullNodeState.Started.ToString();
                    }
                    catch (Exception e) { }

                    return false;

                }, retryDelayInMiliseconds: (int)TimeSpan.FromSeconds(2).TotalMilliseconds);

                // TODO Check that node2/3 can't access the channel.
                // TODO Add node2/3's org to the channel access control and update
                // TODO Then check again

                var orgToAdd = "TestOrg";

                var request = new ChannelUpdateRequest
                {
                    Name = "Channel1",
                    MembersToAdd = new AccessControlList
                    {
                        Organisations = new List<string>
                        {
                            orgToAdd
                        }
                    }
                };

                var channelUpdateTxBuilder = infraNode.FullNode.NodeService<ChannelUpdateTransactionBuilder>();

                var tx = channelUpdateTxBuilder.Build(request);
                await infraNode.BroadcastTransactionAsync(tx);
                TestBase.WaitLoop(() => infraNode2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await infraNode.MineBlocksAsync(1);

                // This approach doesn't work, try building the tx directly instead.
                //var response = await $"http://localhost:{infraNode.SystemChannelApiPort}/api".AppendPathSegment("channels/update").PostJsonAsync(request);
                //Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                // Create a channel update request transaction
                // Mine a block
                // Check that the organisation has been addedd
                //Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

                //await node1.BroadcastTransactionAsync(callTransaction);
                //TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                //await node1.MineBlocksAsync(1);

            }
        }
    }
}