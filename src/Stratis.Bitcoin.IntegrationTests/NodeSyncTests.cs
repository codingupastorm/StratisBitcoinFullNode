using System;
using System.Collections.Generic;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core.Networks;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;
using System.Linq;
using NBitcoin.PoA;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeSyncTests
    {
        private static List<string> FederationPermissions = new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission };
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

                CoreNode miner = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "ns-5-miner", permissions: FederationPermissions).Start();
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "ns-5-syncer", permissions: FederationPermissions).Start();
                CoreNode reorg = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "ns-5-reorg", permissions: FederationPermissions).Start();

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
        public void MiningNodeWithOneConnection_AlwaysSynced()
        {
            var network = new TokenlessNetwork();

            // Add one more federation member.
            var mnemonics = new List<Mnemonic>(TokenlessNetwork.Mnemonics);
            mnemonics.Add(new Mnemonic(Wordlist.English, WordCount.Twelve));
            network.FederationKeys = mnemonics.Select(m => TokenlessNetwork.FederationKeyFromMnemonic(m)).ToArray();
            var genesisFederationMembers = network.FederationKeys.Select(k => (IFederationMember)new FederationMember(k.PubKey)).ToList();
            (network.Consensus.Options as PoAConsensusOptions).GenesisFederationMembers = genesisFederationMembers;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                CoreNode minerNode = nodeBuilder.CreateTokenlessNode(network, 0, server, permissions: FederationPermissions).Start();
                CoreNode connectorNode = nodeBuilder.CreateTokenlessNode(network, 1, server, permissions: FederationPermissions).Start();
                CoreNode firstNode = nodeBuilder.CreateTokenlessNode(network, 2, server, permissions: FederationPermissions).Start();
                CoreNode secondNode = nodeBuilder.CreateTokenlessNode(network, 3, server, permissions: FederationPermissions, 
                    configParameters: new NodeConfigParameters() { { "mnemonic", mnemonics[3].ToString() } }).Start();

                TestHelper.Connect(minerNode, connectorNode);
                TestHelper.Connect(connectorNode, firstNode);
                TestHelper.Connect(connectorNode, secondNode);
                TestHelper.Connect(firstNode, secondNode);

                List<CoreNode> nodes = new List<CoreNode> { minerNode, connectorNode, firstNode, secondNode };

                nodes.ForEach(n =>
                {
                    n.MineBlocksAsync(1).GetAwaiter().GetResult();
                    TestHelper.WaitForNodeToSync(nodes.ToArray());
                });

                Assert.Equal(nodes.Count, minerNode.FullNode.ChainIndexer.Height);

                // Random node on network generates a block.
                firstNode.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestHelper.WaitForNodeToSync(firstNode, connectorNode, secondNode, minerNode);

                // Miner mines the block.
                minerNode.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestHelper.WaitForNodeToSync(minerNode, connectorNode);

                // Connector node mines a block.
                connectorNode.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestHelper.WaitForNodeToSync(nodes.ToArray());
            }
        }
    }
}
