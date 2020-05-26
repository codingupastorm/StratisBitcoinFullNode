using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Newtonsoft.Json.Linq;
using Stratis.Core.Connection;
using Stratis.Core.Controllers.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Stratis.Features.Api;
using Xunit.Abstractions;
using Stratis.Feature.PoA.Tokenless.Networks;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";

        // BlockStore
        private const string GetBlockCountUri = "api/blockstore/getblockcount";

        // ConnectionManager
        private const string AddnodeUri = "api/connectionmanager/addnode";
        private const string GetPeerInfoUri = "api/connectionmanager/getpeerinfo";

        // Consensus
        private const string GetBestBlockHashUri = "api/consensus/getbestblockhash";
        private const string GetBlockHashUri = "api/consensus/getblockhash";

        // Node
        private const string GetBlockHeaderUri = "api/node/getblockheader";
        private const string StatusUri = "api/node/status";

        private IWebHost server;

        private CoreNode stratisApiNode;
        private CoreNode secondStratisApiNode;

        private HttpResponseMessage response;
        private string responseText;

        private int maturity = 1;
        private SmartContractNodeBuilder nodeBuilder;

        private Uri apiUri;
        private HttpClient httpClient;
        private HttpClientHandler httpHandler;
        private TokenlessNetwork network;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            this.server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build();
            this.nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder);

            this.server.Start();

            // Start + Initialize CA.
            var client = TokenlessTestHelper.GetAdminClient(this.server);
            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

            this.httpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true };
            this.httpClient = new HttpClient(this.httpHandler);
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            this.network = new TokenlessNetwork();
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            if (this.server != null)
            {
                this.server.Dispose();
                this.server = null;
            }

            if (this.httpHandler != null)
            {
                this.httpHandler.Dispose();
                this.httpHandler = null;
            }

            this.nodeBuilder.Dispose();
        }

        private void two_connected_proof_of_work_nodes_with_api_enabled()
        {
            a_proof_of_work_node_with_api_enabled();
            a_second_proof_of_work_node_with_api_enabled();
            calling_addnode_connects_two_nodes();
        }

        private void a_proof_of_work_node_with_api_enabled()
        {

            // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
            this.stratisApiNode = this.nodeBuilder.CreateTokenlessNode(this.network, 0, this.server).Start();

            //this.firstStratisPowApiNode = this.powNodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
            this.stratisApiNode.Mnemonic = this.stratisApiNode.Mnemonic;

            this.apiUri = this.stratisApiNode.FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_second_proof_of_work_node_with_api_enabled()
        {
            this.secondStratisApiNode = this.nodeBuilder.CreateTokenlessNode(this.network, 1, this.server).Start();
            this.secondStratisApiNode.Mnemonic = this.secondStratisApiNode.Mnemonic;
        }

        protected void a_block_is_mined()
        {
            this.stratisApiNode.MineBlocksAsync(1).GetAwaiter().GetResult();
        }

        private void more_blocks_mined_past_maturity_of_original_block()
        {
            this.stratisApiNode.MineBlocksAsync(this.maturity).GetAwaiter().GetResult();
        }

        private void calling_addnode_connects_two_nodes()
        {
            this.send_api_get_request($"{AddnodeUri}?endpoint={this.secondStratisApiNode.Endpoint.ToString()}&command=onetry");
            this.responseText.Should().Be("true");

            TestBase.WaitLoop(() => TestHelper.AreNodesSynced(this.stratisApiNode, this.secondStratisApiNode));
        }

        private void calling_getblockcount()
        {
            this.send_api_get_request(GetBlockCountUri);
        }

        private void calling_getbestblockhash()
        {
            this.send_api_get_request(GetBestBlockHashUri);
        }

        private void calling_getpeerinfo()
        {
            this.send_api_get_request(GetPeerInfoUri);
        }

        private void calling_getblockhash()
        {
            this.send_api_get_request($"{GetBlockHashUri}?height=0");
        }

        private void calling_getblockheader()
        {
            this.send_api_get_request($"{GetBlockHeaderUri}?hash={this.network.Consensus.HashGenesisBlock.ToString()}");
        }

        private void calling_status()
        {
            this.send_api_get_request(StatusUri);
        }

        private void the_consensus_tip_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.stratisApiNode.FullNode.ConsensusManager().Tip.HashBlock.ToString() + "\"");
        }

        private void the_blockcount_should_match_consensus_tip_height()
        {
            this.responseText.Should().Be(this.stratisApiNode.FullNode.ConsensusManager().Tip.Height.ToString());
        }

        private void the_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.network.Consensus.HashGenesisBlock.ToString() + "\"");
        }

        private void status_information_is_returned()
        {
            var statusNode = this.stratisApiNode.FullNode;
            var statusResponse = JsonDataSerializer.Instance.Deserialize<NodeStatusModel>(this.responseText);
            statusResponse.Agent.Should().Contain(statusNode.Settings.Agent);
            statusResponse.Version.Should().Be(statusNode.Version.ToString());
            statusResponse.Network.Should().Be(statusNode.Network.Name);
            statusResponse.ConsensusHeight.Should().Be(0);
            statusResponse.BlockStoreHeight.Should().Be(0);
            statusResponse.ProtocolVersion.Should().Be((uint)(statusNode.Settings.ProtocolVersion));
            statusResponse.DataDirectoryPath.Should().Be(statusNode.Settings.DataDir);

            List<string> featuresNamespaces = statusResponse.FeaturesData.Select(f => f.Namespace).ToList();
            featuresNamespaces.Should().Contain("Stratis.Core.Base.BaseFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.Api.ApiFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.BlockStore.BlockStoreFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.Consensus.ConsensusFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.MemoryPool.MempoolFeature");
            featuresNamespaces.Should().Contain("Stratis.Feature.PoA.Tokenless.KeyStore.TokenlessKeyStoreFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.SmartContracts.SmartContractFeature");
            featuresNamespaces.Should().Contain("Stratis.Feature.PoA.Tokenless.TokenlessFeature");

            statusResponse.FeaturesData.All(f => f.State == "Initialized").Should().BeTrue();
        }

        private void the_blockheader_is_returned()
        {
            var blockheaderResponse = JsonDataSerializer.Instance.Deserialize<BlockHeaderModel>(this.responseText);
            blockheaderResponse.PreviousBlockHash.Should()
                .Be("0000000000000000000000000000000000000000000000000000000000000000");
        }

        private void a_single_connected_peer_is_returned()
        {
            List<PeerNodeModel> getPeerInfoResponseList = JArray.Parse(this.responseText).ToObject<List<PeerNodeModel>>();
            getPeerInfoResponseList.Count.Should().Be(1);
            getPeerInfoResponseList[0].Id.Should().Be(0);
            getPeerInfoResponseList[0].Address.Should().Contain("[::ffff:127.0.0.1]");
        }

        private void send_api_get_request(string apiendpoint)
        {
            this.response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();
            if (this.response.IsSuccessStatusCode)
            {
                this.responseText = this.response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }
    }
}
