using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Stratis.Features.Api;
using Stratis.Features.Wallet.Models;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification : BddSpecification
    {
        private const string JsonContentType = "application/json";
        private const string WalletName = "mywallet";
        private const string WalletPassword = "password";
        private const string WalletPassphrase = "wallet_passphrase";

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
        private const string ValidateAddressUri = "api/node/validateaddress";

        // Wallet
        private const string GeneralInfoUri = "api/wallet/general-info";

        private CoreNode stratisPosApiNode;
        private CoreNode firstStratisPowApiNode;
        private CoreNode secondStratisPowApiNode;

        private HttpResponseMessage response;
        private string responseText;

        private int maturity = 1;
        private NodeBuilder powNodeBuilder;
        private NodeBuilder posNodeBuilder;

        private Uri apiUri;
        private HttpClient httpClient;
        private HttpClientHandler httpHandler;
        private Network powNetwork;
        private Network posNetwork;

        public ApiSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.httpHandler = new HttpClientHandler() { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => true };
            this.httpClient = new HttpClient(this.httpHandler);
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));

            this.powNodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
            this.posNodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));

            this.powNetwork = new BitcoinRegTestOverrideCoinbaseMaturity(1);
            this.posNetwork = new StratisRegTest();
        }

        protected override void AfterTest()
        {
            if (this.httpClient != null)
            {
                this.httpClient.Dispose();
                this.httpClient = null;
            }

            if (this.httpHandler != null)
            {
                this.httpHandler.Dispose();
                this.httpHandler = null;
            }

            this.powNodeBuilder.Dispose();
            this.posNodeBuilder.Dispose();
        }

        private void two_connected_proof_of_work_nodes_with_api_enabled()
        {
            a_proof_of_work_node_with_api_enabled();
            a_second_proof_of_work_node_with_api_enabled();
            calling_addnode_connects_two_nodes();
        }

        private void a_proof_of_work_node_with_api_enabled()
        {
            this.firstStratisPowApiNode = this.powNodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
            this.firstStratisPowApiNode.Mnemonic = this.firstStratisPowApiNode.Mnemonic;

            this.firstStratisPowApiNode.FullNode.Network.Consensus.ConsensusMiningReward.CoinbaseMaturity = this.maturity;
            this.apiUri = this.firstStratisPowApiNode.FullNode.NodeService<ApiSettings>().ApiUri;
        }

        private void a_second_proof_of_work_node_with_api_enabled()
        {
            this.secondStratisPowApiNode = this.powNodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
            this.secondStratisPowApiNode.Mnemonic = this.secondStratisPowApiNode.Mnemonic;
        }

        protected void a_block_is_mined_creating_spendable_coins()
        {
            TestHelper.MineBlocks(this.firstStratisPowApiNode, 1);
        }

        private void more_blocks_mined_past_maturity_of_original_block()
        {
            TestHelper.MineBlocks(this.firstStratisPowApiNode, this.maturity);
        }

        private void calling_addnode_connects_two_nodes()
        {
            this.send_api_get_request($"{AddnodeUri}?endpoint={this.secondStratisPowApiNode.Endpoint.ToString()}&command=onetry");
            this.responseText.Should().Be("true");

            TestBase.WaitLoop(() => TestHelper.AreNodesSynced(this.firstStratisPowApiNode, this.secondStratisPowApiNode));
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
            this.send_api_get_request($"{GetBlockHeaderUri}?hash={KnownNetworks.RegTest.Consensus.HashGenesisBlock.ToString()}");
        }

        private void calling_status()
        {
            this.send_api_get_request(StatusUri);
        }

        private void calling_validateaddress()
        {
            string address = this.firstStratisPowApiNode.FullNode.WalletManager()
                .GetUnusedAddress()
                .ScriptPubKey.GetDestinationAddress(this.firstStratisPowApiNode.FullNode.Network).ToString();
            this.send_api_get_request($"{ValidateAddressUri}?address={address}");
        }

        private void the_consensus_tip_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + this.firstStratisPowApiNode.FullNode.ConsensusManager().Tip.HashBlock.ToString() + "\"");
        }

        private void the_blockcount_should_match_consensus_tip_height()
        {
            this.responseText.Should().Be(this.firstStratisPowApiNode.FullNode.ConsensusManager().Tip.Height.ToString());
        }

        private void the_blockhash_is_returned()
        {
            this.responseText.Should().Be("\"" + KnownNetworks.RegTest.Consensus.HashGenesisBlock.ToString() + "\"");
        }

        private void status_information_is_returned()
        {
            var statusNode = this.firstStratisPowApiNode.FullNode;
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
            featuresNamespaces.Should().Contain("Stratis.Features.Consensus.PowConsensusFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.MemoryPool.MempoolFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.Miner.MiningFeature");
            featuresNamespaces.Should().Contain("Stratis.Features.Wallet.WalletFeature");

            statusResponse.FeaturesData.All(f => f.State == "Initialized").Should().BeTrue();
        }

        private void general_information_about_the_wallet_and_node_is_returned()
        {
            var generalInfoResponse = JsonDataSerializer.Instance.Deserialize<WalletGeneralInfoModel>(this.responseText);
            generalInfoResponse.WalletName.Should().Be(WalletName);
            generalInfoResponse.Network.Name.Should().Be(this.powNetwork.Name);
            generalInfoResponse.ChainTip.Should().Be(0);
            generalInfoResponse.IsChainSynced.Should().BeFalse();
            generalInfoResponse.ConnectedNodes.Should().Be(0);
            generalInfoResponse.IsDecrypted.Should().BeTrue();
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
