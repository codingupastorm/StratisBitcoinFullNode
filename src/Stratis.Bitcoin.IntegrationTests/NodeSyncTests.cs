using System;
using System.Collections.Generic;
using System.IO;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        private readonly Network powNetwork;

        public NodeSyncTests()
        {
            this.powNetwork = new BitcoinRegTest();
        }

        public class StratisRegTestMaxReorg : StratisRegTest
        {
            public StratisRegTestMaxReorg()
            {
                this.Name = Guid.NewGuid().ToString();

                Type consensusType = typeof(NBitcoin.Consensus);
                consensusType.GetProperty("MaxReorgLength").SetValue(this.Consensus, (uint)10);
            }
        }

        [Fact]
        public void TokenlessNodesAreSyncedBigReorgHappensReorgIsIgnored()
        {
            var network = new TokenlessNetwork();
            Type consensusType = typeof(NBitcoin.Consensus);
            consensusType.GetProperty("MaxReorgLength").SetValue(network.Consensus, (uint)10);

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode miner = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "ns-5-miner", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "ns-5-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                CoreNode reorg = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "ns-5-reorg", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();

                miner.MineBlocksAsync(1).GetAwaiter().GetResult();

                // Sync miner with syncer and reorg
                TestHelper.ConnectAndSync(miner, reorg);
                TestHelper.ConnectAndSync(miner, syncer);

                // Create a reorg by mining on two different chains
                TestHelper.Disconnect(miner, reorg);
                TestHelper.Disconnect(miner, syncer);

                miner.MineBlocksAsync(11).GetAwaiter().GetResult();
                reorg.MineBlocksAsync(12).GetAwaiter().GetResult();

                // Make sure the nodes are actually on different chains.
                Assert.NotEqual(miner.FullNode.ChainIndexer.GetHeader(2).HashBlock, reorg.FullNode.ChainIndexer.GetHeader(2).HashBlock);

                TestHelper.ConnectAndSync(miner, syncer);

                // The hash before the reorg node is connected.
                uint256 hashBeforeReorg = miner.FullNode.ChainIndexer.Tip.HashBlock;

                // Connect the reorg chain
                TestHelper.ConnectNoCheck(miner, reorg);
                TestHelper.ConnectNoCheck(syncer, reorg);

                // Wait for the synced chain to get headers updated.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(reorg));

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(miner, syncer));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(reorg, miner) == false);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(reorg, syncer) == false);

                // Check that a reorg did not happen.
                Assert.Equal(hashBeforeReorg, syncer.FullNode.ChainIndexer.Tip.HashBlock);
            }
        }

        /// <summary>
        /// This test simulates scenario from issue #862.
        /// <para>
        /// Connection scheme:
        /// Network - Node1 - MiningNode
        /// </para>
        /// </summary>
        [Fact]
        public void Pow_MiningNodeWithOneConnection_AlwaysSynced()
        {
            string testFolderPath = Path.Combine(this.GetType().Name, nameof(Pow_MiningNodeWithOneConnection_AlwaysSynced));

            using (NodeBuilder nodeBuilder = NodeBuilder.Create(testFolderPath))
            {
                CoreNode minerNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                CoreNode connectorNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                CoreNode firstNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                CoreNode secondNode = nodeBuilder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();

                TestHelper.Connect(minerNode, connectorNode);
                TestHelper.Connect(connectorNode, firstNode);
                TestHelper.Connect(connectorNode, secondNode);
                TestHelper.Connect(firstNode, secondNode);

                List<CoreNode> nodes = new List<CoreNode> { minerNode, connectorNode, firstNode, secondNode };

                nodes.ForEach(n =>
                {
                    TestHelper.MineBlocks(n, 1);
                    TestHelper.WaitForNodeToSync(nodes.ToArray());
                });

                Assert.Equal(minerNode.FullNode.ChainIndexer.Height, nodes.Count);

                // Random node on network generates a block.
                TestHelper.MineBlocks(firstNode, 1);
                TestHelper.WaitForNodeToSync(firstNode, connectorNode, secondNode);

                // Miner mines the block.
                TestHelper.MineBlocks(minerNode, 1);
                TestHelper.WaitForNodeToSync(minerNode, connectorNode);

                TestHelper.MineBlocks(connectorNode, 1);

                TestHelper.WaitForNodeToSync(nodes.ToArray());
            }
        }
    }
}
