using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Features.Wallet;
using Stratis.Features.Wallet.Controllers;
using Stratis.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common.TestFramework;
using Xunit.Abstractions;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Stratis.Feature.PoA.Tokenless.Networks;
using CertificateAuthority;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class RetrieveFromBlockStoreSpecification : BddSpecification
    {
        private CaTester caTester = new CaTester();
        private IWebHost server;
        private SmartContractNodeBuilder builder;
        private CoreNode node;
        private List<uint256> blockIds;
        private IList<Block> retrievedBlocks;
        private const string password = "password";
        private const string walletName = "mywallet";
        private uint256 wrongBlockId;
        private IEnumerable<uint256> retrievedBlockHashes;
        private CoreNode transactionNode;
        private readonly Money transferAmount = Money.COIN * 2;
        private Transaction transaction;
        private uint256 blockWithTransactionId;
        private Transaction retrievedTransaction;
        private uint256 wrongTransactionId;
        private Transaction wontRetrieveTransaction;
        private uint256 retrievedBlockId;
        private Transaction wontRetrieveBlockId;
        private readonly TokenlessNetwork network = new TokenlessNetwork();
        private string caBaseAddress => this.caTester.CABaseAddress;

        public RetrieveFromBlockStoreSpecification(ITestOutputHelper output) : base(output) {}

        protected override void BeforeTest()
        {
            string testRootFolder = Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName);
            this.server = CaTestHelper.CreateWebHostBuilder(testRootFolder, this.caBaseAddress).Build();
            this.builder = SmartContractNodeBuilder.Create(testRootFolder);
        }

        protected override void AfterTest()
        {
            this.builder?.Dispose();
            this.server?.Dispose();
        }

        private void a_ca_node_running()
        {
            this.server.Start();

            // Start + Initialize CA.
            var client = TokenlessTestHelper.GetAdminClient(this.caBaseAddress);

            client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network).Should().BeTrue();
        }

        private void a_poa_node_running()
        {
            this.node = this.builder.CreateTokenlessNode(this.network, 1, this.server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, caBaseAddress: this.caBaseAddress).Start();
        }

        private void a_poa_node_to_transact_with()
        {
            this.transactionNode = this.builder.CreateTokenlessNode(this.network, 1, this.server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, caBaseAddress: this.caBaseAddress).Start();

            TestHelper.Connect(this.transactionNode, this.node);
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);
        }

        private void a_miner_validating_blocks()
        {
        }

        private void some_real_blocks_with_a_uint256_identifier()
        {
            this.node.MineBlocksAsync(1);

            this.blockIds = new List<uint256> { this.node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock };
        }

        private void some_blocks_creating_reward()
        {
            this.some_real_blocks_with_a_uint256_identifier();
        }

        private void a_wrong_block_id()
        {
            this.wrongBlockId = new uint256(3141592653589793238);
            this.blockIds.Should().NotContain(this.wrongBlockId, "it would corrupt the test");
        }

        private void a_wrong_transaction_id()
        {
            this.wrongTransactionId = new uint256(314159265358979323);
            this.transaction.GetHash().Should().NotBe(this.wrongTransactionId, "it would corrupt the test");
        }

        private void the_node_is_synced()
        {
            TestHelper.WaitForNodeToSync(this.node);
        }

        private void the_nodes_are_synced()
        {
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);
        }

        private void a_real_transaction()
        {
            // TODO: Build the transaction.

            this.node.FullNode.NodeController<WalletController>()
                .SendTransaction(new SendTransactionRequest(this.transaction.ToHex()));
        }

        private void the_block_with_the_transaction_is_mined()
        {
            this.blockWithTransactionId = TestHelper.MineBlocks(this.node, 2).BlockHashes[0];
            TestHelper.WaitForNodeToSync(this.node, this.transactionNode);
        }

        private void trying_to_retrieve_the_blocks_from_the_blockstore()
        {
            this.retrievedBlocks = this.blockIds.Concat(new[] { this.wrongBlockId })
                .Select(id => this.node.FullNode.BlockStore().GetBlock(id)).Select(b => b).ToList();

            this.retrievedBlocks.Count(b => b != null).Should().Be(this.blockIds.Count);
            this.retrievedBlocks.Count(b => b == null).Should().Be(1);
            this.retrievedBlockHashes = this.retrievedBlocks.Where(b => b != null).Select(b => b.GetHash());

            this.retrievedBlockHashes.Should().OnlyHaveUniqueItems();
        }

        private void trying_to_retrieve_the_transactions_by_Id_from_the_blockstore()
        {
            this.retrievedTransaction = this.node.FullNode.BlockStore().GetTransactionById(this.transaction.GetHash());
            this.wontRetrieveTransaction = this.node.FullNode.BlockStore().GetTransactionById(this.wrongTransactionId);
        }

        private void trying_to_retrieve_the_block_containing_the_transactions_from_the_blockstore()
        {
            this.retrievedBlockId = this.node.FullNode.BlockStore()
                .GetBlockIdByTransactionId(this.transaction.GetHash());
            this.wontRetrieveBlockId = this.node.FullNode.BlockStore()
                .GetTransactionById(this.wrongTransactionId);
        }

        private void real_blocks_should_be_retrieved()
        {
            this.retrievedBlockHashes.Should().BeEquivalentTo(this.blockIds);
        }

        private void the_wrong_block_id_should_return_null()
        {
            this.retrievedBlockHashes.Should().NotContain(this.wrongBlockId);
        }

        private void the_real_transaction_should_be_retrieved()
        {
            this.retrievedTransaction.Should().NotBeNull();
            this.retrievedTransaction.GetHash().Should().Be(this.transaction.GetHash());
            // TODO?
            //this.retrievedTransaction.Outputs.Should()
              //  .Contain(t => t.Value == this.transferAmount.Satoshi
                //              && t.ScriptPubKey.GetDestinationAddress(this.node.FullNode.Network).ScriptPubKey == this.receiverAddress.ScriptPubKey);
        }

        private void the_wrong_transaction_id_should_return_null()
        {
            this.wontRetrieveTransaction.Should().BeNull();
        }

        private void the_block_with_the_real_transaction_should_be_retrieved()
        {
            this.retrievedBlockId.Should().NotBeNull();
            this.retrievedBlockId.Should().Be(this.blockWithTransactionId);
        }

        private void the_block_with_the_wrong_id_should_return_null()
        {
            this.wontRetrieveBlockId.Should().BeNull();
        }
    }
}