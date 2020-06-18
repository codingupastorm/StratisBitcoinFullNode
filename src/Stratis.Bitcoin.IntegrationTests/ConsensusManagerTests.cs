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
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA;
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

        public class FailValidation3_2 : FailValidation
        {
            public FailValidation3_2() : base(3, 2)
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

                // Miner A mines to height 5 on a new and longer chain whilst disconnected.
                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();

                // Enable syncer to send blocks to miner A.
                TestHelper.EnableBlockPropagation(syncer, minerA);

                minerA.MineBlocksAsync(1).GetAwaiter().GetResult();

                // Syncer should switch to the new longest chain...
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 6));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 6));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 6));
            }
        }

        [Fact]
        public void CM_Reorgs_Connect_Longer_Chain_With_Connected_Blocks_Fails_Reverts()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                var syncerNetwork = new TokenlessNetwork();

                // Inject a rule that will fail at block 3 of the new chain.
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation3_2));

                var config = new NodeConfigParameters { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-5-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-5-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(syncerNetwork, 2, server, agent: "syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission }, configParameters: config);

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, syncer);

                // Sync the network to height 2.
                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 4.
                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 4));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 2));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 4));

                // Miner B continues to mine some invalid blocks to height 6 on a new and longer chain.
                minerB.MineBlocksAsync(4).GetAwaiter().GetResult();

                // Confirm miner B at height 6.
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 6));

                // Miner B should become disconnected due to the new chain failing
                // consensus validation.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back.
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 4);

                // Check syncer is still synced with Miner A.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void CMReorgsTryConnectLongerChainNoConnectedBlocksFailsReverts()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                var syncerNetwork = new TokenlessNetwork();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11_2));

                var config = new NodeConfigParameters { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-6-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-6-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(syncerNetwork, 2, server, agent: "syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission }, configParameters: config);

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, syncer);

                // Sync the network to height 10.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Miner B continues to mine to height 30 on a new and longer chain.
                minerB.MineBlocksAsync(20).GetAwaiter().GetResult();

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
        public void CMReorgToLongestChainMultipleTimesWithoutInvalidBlocks()
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
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-7-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "cm-7-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(this.network, 2, server, agent: "cm-7-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission });

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

                syncer.OverrideService(flushCondition);

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, syncer);

                // Sync the network to height 10.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Syncer syncs to minerA's block of 11
                minerA.MineBlocksAsync(1).GetAwaiter().GetResult();
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 11));

                // Syncer jumps chain and reorgs to minerB's longer chain of 12
                minerB.MineBlocksAsync(2).GetAwaiter().GetResult();
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 11));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 12));

                // Syncer jumps chain and reorg to minerA's longer chain of 18
                minerA.MineBlocksAsync(2).GetAwaiter().GetResult();
                TestHelper.TriggerSync(syncer);
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 13));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 12));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 13));
            }
        }

        [Fact]
        public void CM_Connect_New_Block_Failed()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                var syncerNetwork = new TokenlessNetwork();

                // Inject a rule that will fail at block 11 of the new chain
                syncerNetwork.Consensus.ConsensusRules.FullValidationRules.Insert(1, typeof(FailValidation11));

                var config = new NodeConfigParameters { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "cm-8-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(syncerNetwork, 1, server, agent: "cm-8-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission }, configParameters: config);

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, syncer);

                // Sync the network to height 10.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(syncer, minerA);

                // Miner A mines to height 11.
                minerA.MineBlocksAsync(1).GetAwaiter().GetResult();

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
            TokenlessNetwork network = new TokenlessNetwork();

            network.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cm-9-minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "cm-9-minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission });
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "cm-9-syncer", permissions: new List<string>() { CaCertificatesManager.SendPermission });

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB, syncer);

                // Sync the network to height 10.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                TestHelper.DisableBlockPropagation(syncer, minerA);
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A mines 5 blocks to height 15.
                minerA.MineBlocksAsync(5).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA), waitTimeSeconds: 120);

                // Miner B continues mines 10 blocks to a longer chain at height 20.
                minerB.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerB), waitTimeSeconds: 120);

                // Miner A mines an additional 10 blocks to height 25 that will create the longest chain.
                minerA.MineBlocksAsync(9).GetAwaiter().GetResult();
                TestHelper.EnableBlockPropagation(syncer, minerA);
                minerA.MineBlocksAsync(1).GetAwaiter().GetResult();

                TestBase.WaitLoopMessage(() => TestHelper.AreNodesSyncedMessage(syncer, minerA), waitTimeSeconds: 120);

                Assert.True(TestHelper.IsNodeSyncedAtHeight(syncer, 25));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerA, 25));
                Assert.True(TestHelper.IsNodeSyncedAtHeight(minerB, 20));
            }
        }

        [Fact]
        public void CM_Block_That_Failed_Partial_Validation_Is_Rejected()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                var config = new NodeConfigParameters { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(this.network, 0, server, agent: "minerA", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(this.network, 1, server, agent: "minerB", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerC = nodeBuilder.CreateTokenlessNode(this.network, 2, server, agent: "minerC", permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);

                TokenlessTestHelper.ShareCertificatesAndStart(this.network, minerA, minerB, minerC);

                // MinerA mines to height 5.
                minerA.MineBlocksAsync(5).GetAwaiter().GetResult();

                // Connect and sync minerA and minerB.
                TestHelper.ConnectAndSync(minerA, minerB);

                TestHelper.Disconnect(minerA, minerB);

                // Mark block 5 as invalid by changing the signature of the block in memory.
                (minerB.FullNode.ChainIndexer.GetHeader(5).Block.Header as PoABlockHeader).BlockSignature.Signature = new byte[] { 0 };

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