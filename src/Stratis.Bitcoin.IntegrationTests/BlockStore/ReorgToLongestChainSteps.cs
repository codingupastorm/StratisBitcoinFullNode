﻿using System.IO;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit.Abstractions;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public partial class ReorgToLongestChainSpecification
    {
        private NodeBuilder nodeBuilder;
        private Network network;
        private CoreNode jingNode;
        private CoreNode bobNode;
        private CoreNode charlieNode;
        private CoreNode daveNode;
        private Transaction shorterChainTransaction;
        private int jingsBlockHeight;

        private const string AccountZero = "account 0";
        private const string WalletZero = "mywallet";
        private const string WalletPassword = "password";

        public ReorgToLongestChainSpecification(ITestOutputHelper output) : base(output)
        {
        }

        protected override void BeforeTest()
        {
            this.network = KnownNetworks.RegTest;
            this.nodeBuilder = NodeBuilder.Create(Path.Combine(this.GetType().Name, this.CurrentTest.DisplayName));
        }

        protected override void AfterTest()
        {
            this.nodeBuilder.Dispose();
        }

        private void four_miners()
        {
            this.jingNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD();
            this.jingNode.Start();
            this.jingNode.WithWallet();

            this.bobNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD();
            this.bobNode.Start();
            this.bobNode.WithWallet();

            this.charlieNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD();
            this.charlieNode.Start();
            this.charlieNode.WithWallet();

            this.daveNode = this.nodeBuilder.CreateStratisPowNode(this.network).NotInIBD();
            this.daveNode.Start();
            this.daveNode.WithWallet();

            TestHelper.Connect(this.jingNode, this.bobNode);
            TestHelper.Connect(this.bobNode, this.charlieNode);
            TestHelper.Connect(this.charlieNode, this.daveNode);
        }

        private void each_mine_a_block()
        {
            TestHelper.MineBlocks(this.jingNode, 1);
            TestHelper.MineBlocks(this.bobNode, 1);
            TestHelper.MineBlocks(this.charlieNode, 1);
            TestHelper.MineBlocks(this.daveNode, 1);
        }

        private void jing_loses_connection_to_others_but_carries_on_mining()
        {
            this.jingNode.FullNode.ConnectionManager.RemoveNodeAddress(this.bobNode.Endpoint);
            this.jingNode.FullNode.ConnectionManager.RemoveNodeAddress(this.charlieNode.Endpoint);
            this.jingNode.FullNode.ConnectionManager.RemoveNodeAddress(this.daveNode.Endpoint);

            TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(this.jingNode));

            TestHelper.MineBlocks(this.jingNode, 1);

            this.jingsBlockHeight = this.jingNode.FullNode.Chain.Height;
        }

        private void bob_creates_a_transaction_and_broadcasts()
        {
            HdAddress nodeCReceivingAddress = this.GetSecondUnusedAddressToAvoidClashWithMiningAddress(this.charlieNode);

            TransactionBuildContext transactionBuildContext = TestHelper.CreateTransactionBuildContext(
                this.bobNode.FullNode.Network,
                WalletZero,
                AccountZero,
                WalletPassword,
                new[] {
                    new Recipient {
                        Amount = Money.COIN * 1,
                        ScriptPubKey = nodeCReceivingAddress.ScriptPubKey
                    }
                },
                FeeType.Medium
                , 1);

            this.shorterChainTransaction = this.bobNode.FullNode.WalletTransactionHandler().BuildTransaction(transactionBuildContext);
            Money shortChainTransactionFee = this.bobNode.FullNode.WalletTransactionHandler().EstimateFee(transactionBuildContext);

            this.bobNode.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(this.shorterChainTransaction.ToHex()));
        }

        private HdAddress GetSecondUnusedAddressToAvoidClashWithMiningAddress(CoreNode node)
        {
            return node.FullNode.WalletManager()
                .GetUnusedAddresses(new WalletAccountReference(WalletZero, AccountZero), 2)
                .Skip(1).First();
        }

        private void charlie_waits_for_the_trx_and_mines_this_block()
        {
            TestHelper.WaitLoop(() => this.charlieNode.FullNode.MempoolManager().GetTransaction(this.shorterChainTransaction.GetHash()).Result != null);

            TestHelper.MineBlocks(this.charlieNode, 1);
            TestHelper.WaitForNodeToSync(this.bobNode, this.charlieNode, this.daveNode);
        }

        private void dave_confirms_transaction_is_present()
        {
            Transaction transaction = this.daveNode.FullNode.BlockStore().GetTransactionByIdAsync(this.shorterChainTransaction.GetHash()).Result;
            transaction.Should().NotBeNull();
            transaction.GetHash().Should().Be(this.shorterChainTransaction.GetHash());
        }

        private void jings_connection_comes_back()
        {
            this.jingNode.CreateRPCClient().AddNode(this.bobNode.Endpoint);
            TestHelper.WaitForNodeToSyncIgnoreMempool(this.jingNode, this.bobNode, this.charlieNode, this.daveNode);
        }

        private void bob_charlie_and_dave_reorg_to_jings_longest_chain()
        {
            TestHelper.WaitLoop(() => this.bobNode.FullNode.Chain.Height == this.jingsBlockHeight);
            TestHelper.WaitLoop(() => this.charlieNode.FullNode.Chain.Height == this.jingsBlockHeight);
            TestHelper.WaitLoop(() => this.daveNode.FullNode.Chain.Height == this.jingsBlockHeight);
        }

        private void bobs_transaction_from_shorter_chain_is_now_missing()
        {
            this.bobNode.FullNode.BlockStore().GetTransactionByIdAsync(this.shorterChainTransaction.GetHash()).Result
                .Should().BeNull("longest chain comes from selfish miner and shouldn't contain the transaction made on the chain with the other 3 nodes");
        }

        private void bobs_transaction_is_now_in_the_mem_pool()
        {
            this.daveNode.CreateRPCClient().GetRawMempool()
                .Should().Contain(x => x == this.shorterChainTransaction.GetHash(), "transaction should be in the mempool when not mined in a longer chain");
        }

        private void mining_continues_to_maturity_to_allow_spend()
        {
            int coinbaseMaturity = (int)this.bobNode.FullNode.Network.Consensus.CoinbaseMaturity;

            TestHelper.MineBlocks(this.bobNode, coinbaseMaturity);

            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.jingNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.bobNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.charlieNode));
            TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(this.daveNode));

            // Ensure that all the nodes are synced to at least coinbase maturity.
            TestHelper.WaitLoop(() => this.jingNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.bobNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.charlieNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
            TestHelper.WaitLoop(() => this.daveNode.FullNode.ConsensusManager().Tip.Height >= this.charlieNode.FullNode.Network.Consensus.CoinbaseMaturity);
        }

        private void meanwhile_jings_chain_advanced_ahead_of_the_others()
        {
            TestHelper.MineBlocks(this.jingNode, 5);

            this.jingsBlockHeight = this.jingNode.FullNode.Chain.Height;
        }
    }
}
