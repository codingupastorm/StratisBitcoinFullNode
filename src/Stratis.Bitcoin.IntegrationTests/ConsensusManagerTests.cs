using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Base;
using Stratis.Core.Connection;
using Stratis.Core.Consensus;
using Stratis.Core.Consensus.Rules;
using Stratis.Core.Interfaces;
using Stratis.Core.Networks;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private readonly TokenlessNetwork network;

        public ConsensusManagerTests()
        {
            this.network = new TokenlessNetwork();
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public ConsensusOptionsTest() : base(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5)
            {
            }

            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 55 ? 50 : 60;
            }
        }

        public class StratisConsensusOptionsOverrideTest : StratisRegTest
        {
            public StratisConsensusOptionsOverrideTest()
            {
                this.Name = Guid.NewGuid().ToString("N").Substring(0, 7);
            }
        }

        public class BitcoinMaxReorgOverrideTest : BitcoinRegTest
        {
            public BitcoinMaxReorgOverrideTest()
            {
                this.Name = Guid.NewGuid().ToString();

                Type consensusType = typeof(NBitcoin.Consensus);
                consensusType.GetProperty("MaxReorgLength").SetValue(this.Consensus, (uint)20);
            }
        }

        public class BitcoinOverrideRegTest : BitcoinRegTest
        {
            public BitcoinOverrideRegTest() : base()
            {
                this.Name = Guid.NewGuid().ToString("N").Substring(0, 7);
            }
        }

        public class FailValidation15_2 : FailValidation
        {
            public FailValidation15_2() : base(15, 2)
            {
            }
        }

        public class FailValidation11 : FailValidation
        {
            public FailValidation11() : base(11)
            {
            }
        }

        public class FailValidation11_2 : FailValidation
        {
            public FailValidation11_2() : base(11, 2)
            {
            }
        }

        public class FailValidation : FullValidationConsensusRule
        {
            /// <summary>
            /// Fail at this height if <see cref="failOnAttemptCount"/> is zero, otherwise decrement it.
            /// </summary>
            private readonly int failOnHeight;

            /// <summary>
            /// The number of blocks at height <see cref="failOnHeight"/> that need to pass before an error is thrown.
            /// </summary>
            private int failOnAttemptCount;

            public FailValidation(int failOnHeight, int failOnAttemptCount = 1)
            {
                this.failOnHeight = failOnHeight;
                this.failOnAttemptCount = failOnAttemptCount;
            }

            public override Task RunAsync(RuleContext context)
            {
                if (this.failOnAttemptCount > 0)
                {
                    if (context.ValidationContext.ChainedHeaderToValidate.Height == this.failOnHeight)
                    {
                        this.failOnAttemptCount -= 1;

                        if (this.failOnAttemptCount == 0)
                        {
                            throw new ConsensusErrorException(new ConsensusError("ConsensusManagerTests-FailValidation-Error", "ConsensusManagerTests-FailValidation-Error"));
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }

        [Fact]
        public void CM_Fork_Occurs_Node_Reorgs_AndResyncs_ToBestHeight()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-1-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-1-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(this.network, 2, server, agent: "cm-1-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission });

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, syncer);

                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA);
                TestHelper.ConnectAndSync(syncer, minerB);

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // Miner A continues to mine to height 15 whilst disconnected.
                minerA.MineBlocksAsync(5).GetAwaiter().GetResult();

                // Miner B continues to mine to height 12 whilst disconnected.
                minerB.MineBlocksAsync(2).GetAwaiter().GetResult();

                // Syncer now connects to both miners causing a re-org to occur for Miner B back to height 10
                TestHelper.Connect(minerA, syncer);
                TestHelper.Connect(minerB, minerA);

                // Ensure that Syncer has synced with Miner A and Miner B.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerA, syncer));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerB, minerA));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 15));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 15));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 15));
            }
        }

        [Fact]
        public void CMForksNodesDisconnectsDueToMaxReorgViolation()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-3-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-3-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB);

                // MinerA mines height 10.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                // Disconnect minerA from minerB.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 20 (10 + 10).
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();

                // MinerB continues to mine to height 40 (10 + 30).
                minerB.MineBlocksAsync(30).GetAwaiter().GetResult();

                // Ensure the correct height before the connect.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 40));

                // Connect minerA to minerB.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // Wait until the nodes become disconnected due to the MaxReorgViolation.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Check that the heights did not change.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 40));
            }
        }

        [Fact]
        public void CM_Reorgs_Then_Old_Chain_Becomes_Longer_Then_Reorg_Back()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-4-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-4-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(this.network, 2, server, agent: "cm-4-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission });

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, syncer);

                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();

                // Sync the network to height 2.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 3.
                minerA.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Disable syncer from sending blocks to miner A
                TestHelper.DisableBlockPropagation(syncer, minerA);

                // Miner B continues to mine to height 4 on a new and longer chain whilst disconnected.
                minerB.MineBlocksAsync(2).GetAwaiter().GetResult();

                // Enable syncer to send blocks to miner B.
                TestHelper.EnableBlockPropagation(syncer, minerB);

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Enable syncer to send blocks to miner A.
                TestHelper.EnableBlockPropagation(syncer, minerA);

                // Miner A mines to height 5.
                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();

                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 5));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 5));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 5));
            }
        }

        [Fact]
        public void CM_Reorgs_Connect_Longer_Chain_With_Connected_Blocks_Fails_Reverts()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 15 of the new chain.
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation15_2));

                var minerA = builder.CreateStratisPowNode(this.network, "cm-5-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.network, "cm-5-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-5-syncer").Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 20));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 20));

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 30));

                // Miner B should become disconnected.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back.
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void CMReorgsTryConnectLongerChainNoConnectedBlocksFailsReverts()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11_2));

                var minerA = builder.CreateStratisPowNode(this.network, "cm-6-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.network, "cm-6-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Listener).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-6-syncer").Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 30));

                // Miner B should become disconnected.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 20));

                // Check syncer is still synced with Miner A
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void CM_Reorg_To_Longest_Chain_Multiple_Times_Without_Invalid_Blocks()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.network, "cm-7-minerA").WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.network, "cm-7-minerB").WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.network, "cm-7-syncer");

                void flushCondition(IServiceCollection services)
                {
                    ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(IBlockStoreQueueFlushCondition));
                    if (service != null)
                        services.Remove(service);

                    services.AddSingleton<IBlockStoreQueueFlushCondition>((serviceprovider) =>
                    {
                        var chainState = serviceprovider.GetService<IChainState>();
                        return new BlockStoreQueueFlushConditionReorgTests(chainState, 10);
                    });
                };

                syncer.OverrideService(flushCondition).Start();

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Syncer syncs to minerA's block of 11
                TestHelper.MineBlocks(minerA, 1);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 11));

                // Syncer jumps chain and reorgs to minerB's longer chain of 12
                TestHelper.MineBlocks(minerB, 2);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 12));

                // Syncer jumps chain and reorg to minerA's longer chain of 18
                TestHelper.MineBlocks(minerA, 2);
                TestHelper.TriggerSync(syncer);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 13));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 13));
            }
        }

        [Fact]
        public void CM_Connect_New_Block_Failed()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11));

                var minerA = builder.CreateStratisPowNode(this.network, "cm-8-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var syncer = builder.CreateStratisPowNode(syncerNetwork, "cm-8-syncer").Start();

                // Miner A mines to height 11.
                TestHelper.MineBlocks(minerA, 1);

                // Connect syncer to Miner A, reorg should fail.
                TestHelper.ConnectNoCheck(syncer, minerA);

                // Syncer should disconnect from miner A after the failed block.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerA));

                // Make sure syncer rolled back
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 10));
            }
        }

        [Fact]
        public void CM_Fork_Of_100_Blocks_Occurs_Node_Reorgs_And_Resyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.network, "cm-9-minerA").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.network, "cm-9-minerB").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Listener).Start();
                var syncer = builder.CreateStratisPowNode(this.network, "cm-9-syncer").WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest100Listener).Start();

                // Sync the network to height 100.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A mines 105 blocks to height 115.
                TestHelper.MineBlocks(minerA, 5);
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA), waitTimeSeconds: 120);

                // Miner B continues mines 110 blocks to a longer chain at height 120.
                TestHelper.MineBlocks(minerB, 10);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                // Miner A mines an additional 10 blocks to height 125 that will create the longest chain.
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 115));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 115));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 110));
            }
        }

        [Fact]
        public void CM_Block_That_Failed_Partial_Validation_Is_Rejected()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                // MinerA requires a physical wallet to stake with.
                var minerA = builder.CreateStratisPosNode(network, "minerA").WithDummyWallet().Start();
                var minerB = builder.CreateStratisPosNode(network, "minerB").Start();
                var minerC = builder.CreateStratisPosNode(network, "minerC").Start();

                // MinerA mines to height 5.
                TestHelper.MineBlocks(minerA, 5);

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                TestHelper.Disconnect(minerA, minerB);

                // Mark block 5 as invalid by changing the signature of the block in memory.
                (minerB.FullNode.ChainIndexer.GetHeader(5).Block as PosBlock).BlockSignature.Signature = new byte[] { 0 };

                // Connect and sync minerB and minerC.
                TestHelper.ConnectNoCheck(minerB, minerC);

                // TODO: when signaling failed blocks is enabled we should check this here.

                // Wait for the nodes to disconnect due to invalid block.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerB, minerC));

                Assert.True(minerC.FullNode.NodeService<IPeerBanning>().IsBanned(minerB.Endpoint));

                minerC.FullNode.NodeService<IPeerBanning>().UnBanPeer(minerA.Endpoint);

                TestHelper.ConnectAndSync(minerC, minerA);

                TestBase.WaitLoop(() => TestHelper.AreNodesSyncedMessage(minerA, minerC).Passed);
            }
        }
    }

    public class BlockStoreQueueFlushConditionReorgTests : IBlockStoreQueueFlushCondition
    {
        private readonly IChainState chainState;
        private readonly int interceptAtBlockHeight;

        public BlockStoreQueueFlushConditionReorgTests(IChainState chainState, int interceptAtBlockHeight)
        {
            this.chainState = chainState;
            this.interceptAtBlockHeight = interceptAtBlockHeight;
        }

        public bool ShouldFlush
        {
            get
            {
                if (this.chainState.ConsensusTip.Height >= this.interceptAtBlockHeight)
                    return false;

                return this.chainState.IsAtBestChainTip;
            }
        }
    }
}