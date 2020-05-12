using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.BlockStore;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public class BlockStoreTests
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly RepositorySerializer repositorySerializer;

        public BlockStoreTests()
        {
            this.loggerFactory = new LoggerFactory();

            this.network = new BitcoinRegTest();
            this.repositorySerializer = new RepositorySerializer(this.network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void BlockRepositoryPutBatch()
        {
            var dataFolder = TestBase.CreateDataFolder(this);

            var keyValueStore = new BlockKeyValueStore(new RepositorySerializer(this.network.Consensus.ConsensusFactory), dataFolder, this.loggerFactory, DateTimeProvider.Default);

            using (var blockRepository = new BlockRepository(this.network, this.loggerFactory, keyValueStore, this.repositorySerializer))
            {
                blockRepository.SetTxIndex(true);

                var blocks = new List<Block>();
                for (int i = 0; i < 5; i++)
                {
                    Block block = this.network.CreateBlock();
                    block.AddTransaction(this.network.CreateTransaction());
                    block.AddTransaction(this.network.CreateTransaction());
                    block.Transactions[0].AddInput(new TxIn(Script.Empty));
                    block.Transactions[0].AddOutput(Money.COIN + i * 2, Script.Empty);
                    block.Transactions[1].AddInput(new TxIn(Script.Empty));
                    block.Transactions[1].AddOutput(Money.COIN + i * 2 + 1, Script.Empty);
                    block.UpdateMerkleRoot();
                    block.Header.HashPrevBlock = blocks.Any() ? blocks.Last().GetHash() : this.network.GenesisHash;
                    blocks.Add(block);
                }

                // put
                blockRepository.PutBlocks(new HashHeightPair(blocks.Last().GetHash(), blocks.Count), blocks);

                // check the presence of each block in the repository
                foreach (Block block in blocks)
                {
                    Block received = blockRepository.GetBlock(block.GetHash());
                    Assert.True(block.ToBytes().SequenceEqual(received.ToBytes()));

                    foreach (Transaction transaction in block.Transactions)
                    {
                        Transaction trx = blockRepository.GetTransactionById(transaction.GetHash());
                        Assert.True(trx.ToBytes().SequenceEqual(transaction.ToBytes()));
                    }
                }

                // delete
                blockRepository.Delete(new HashHeightPair(blocks.ElementAt(2).GetHash(), 2), new[] { blocks.ElementAt(2).GetHash() }.ToList());
                Block deleted = blockRepository.GetBlock(blocks.ElementAt(2).GetHash());
                Assert.Null(deleted);
            }
        }

        [Fact(Skip = "Investigate PeerConnector shutdown timeout issue")]
        public void BlockStoreCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network, "bs-2-stratisNodeSync").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();

                // Set the tip of the best chain to some blocks in the past.
                stratisNodeSync.FullNode.ChainIndexer.SetTip(stratisNodeSync.FullNode.ChainIndexer.GetHeader(stratisNodeSync.FullNode.ChainIndexer.Height - 5));

                // Stop the node to persist the chain with the reset tip.
                stratisNodeSync.FullNode.Dispose();

                CoreNode newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // Start the node, this should hit the block store recover code.
                newNodeInstance.Start();

                // Check that the store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.ChainIndexer.Tip.HashBlock, newNodeInstance.FullNode.GetBlockStoreTip().HashBlock);
            }
        }

        [Fact]
        public async Task BlockStoreCanReorgAsync()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode nodeSync = nodeBuilder.CreateTokenlessNode(network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                int maxReorgLength = (int)Math.Min(10, nodeSync.FullNode.ChainBehaviorState.MaxReorgLength);
                await nodeSync.MineBlocksAsync(maxReorgLength);
                TestBase.WaitLoop(() => nodeSync.FullNode.GetBlockStoreTip().Height == maxReorgLength);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 1, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 2, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();

                // Sync both nodes
                TestHelper.ConnectAndSync(nodeSync, node1, node2);

                // All nodes should be at height of maxReorgLength now.

                // Remove node 2.
                TestHelper.Disconnect(nodeSync, node2);

                // Mine a shorter chain with node 1.
                await node1.MineBlocksAsync(maxReorgLength - 1);
                TestBase.WaitLoop(() => node1.FullNode.GetBlockStoreTip().Height == maxReorgLength * 2 - 1);

                // Wait for nodeSync to align with node1.
                TestBase.WaitLoop(() => node1.FullNode.GetBlockStoreTip().HashBlock == nodeSync.FullNode.GetBlockStoreTip().HashBlock);

                // Both node1 and nodeSync should be at height of maxReorgLength * 2 - 1 now.

                // Disconnect all nodes.
                TestHelper.Disconnect(nodeSync, node1);
                TestHelper.Disconnect(nodeSync, node2);

                // Mine a longer chain with node 2.
                await node2.MineBlocksAsync(maxReorgLength);
                TestBase.WaitLoop(() => node2.FullNode.GetBlockStoreTip().Height == maxReorgLength * 2);

                // Reconnect all nodes.
                TestHelper.Connect(nodeSync, node1);
                TestHelper.Connect(nodeSync, node2);

                // Node 2 should be synced.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(node2, nodeSync));
            }
        }

        [Fact]
        public async Task BlockStoreIndexTx()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(network, 1, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(network, 2, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();

                // Mine some blocks with node2
                await node2.MineBlocksAsync(10);
                TestBase.WaitLoop(() => node2.FullNode.GetBlockStoreTip().Height == 10);

                // Sync both nodes.
                TestHelper.ConnectAndSync(node1, node2);

                TestBase.WaitLoop(() => node1.FullNode.GetBlockStoreTip().Height == 10);
                TestBase.WaitLoop(() => node1.FullNode.GetBlockStoreTip().HashBlock == node2.FullNode.GetBlockStoreTip().HashBlock);

                Block bestBlock1 = node1.FullNode.BlockStore().GetBlock(node1.FullNode.ChainIndexer.Tip.HashBlock);
                Assert.NotNull(bestBlock1);

                // Get the block coinbase trx.
                Transaction trx = node2.FullNode.BlockStore().GetTransactionById(bestBlock1.Transactions.First().GetHash());
                Assert.NotNull(trx);
                Assert.Equal(bestBlock1.Transactions.First().GetHash(), trx.GetHash());
            }
        }
    }
}
