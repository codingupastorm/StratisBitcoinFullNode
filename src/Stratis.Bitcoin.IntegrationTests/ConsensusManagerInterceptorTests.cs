using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Xunit;
using Stratis.Feature.PoA.Tokenless.Networks;
using Microsoft.AspNetCore.Hosting;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using System.Collections.Generic;
using CertificateAuthority;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using NBitcoin;
using Stratis.Features.PoA;

namespace Stratis.Bitcoin.IntegrationTests
{
    public sealed class ConsensusManagerInterceptorTests
    {
        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work but containing an invalid block.
        /// </summary>
        [Fact]
        public async Task ReorgChain_Scenario1_Async()
        {
            var network = new TokenlessNetwork();
            var networkNoValidationRules = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                var config = new NodeConfigParameters() { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(networkNoValidationRules, 1, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 2, server, permissions: new List<string>() { CaCertificatesManager.SendPermission }, configParameters: config);

                bool minerADisconnectedFromSyncer = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromSyncer)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tip has rewound to 10.
                        TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, syncer);
                        minerADisconnectedFromSyncer = true;

                        return;
                    }
                }

                // Start the nodes.
                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB, syncer);

                // Start minerA and mine 10 blocks. We cannot use a premade chain as it adversely affects the max tip age calculation, causing sporadic sync errors.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                minerA.SetDisconnectInterceptor(interceptor);

                // minerB and syncer syncs with minerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disconnect minerB from miner so that it can mine on its own and create a fork.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 14.
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // minerB mines 5 more blocks:
                // Block 11,12,14,15 = valid
                // Block 13 = invalid
                Assert.False(TestHelper.IsNodeConnected(minerB));

                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();

                // Mark block 13 as invalid by changing the signature of the block in memory.
                ((minerB.FullNode.ChainIndexer.GetHeader(13).Block as Block).Header as PoABlockHeader).BlockSignature.Signature = new byte[] { 0 };

                // Reconnect minerA to minerB.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // minerB should be disconnected from minerA.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // syncer should be disconnected from minerA (via interceptor).
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, syncer));

                // The reorg will fail at block 8 and roll back any changes.
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 14));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(syncer, 14));
            }
        }

        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work.
        /// </summary>
        [Fact]
        public void ReorgChain_Scenario2()
        {
            var network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                var config = new NodeConfigParameters() { { "bantime", "120" } };

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(network, 1, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }, configParameters: config);
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 2, server, permissions: new List<string>() { CaCertificatesManager.SendPermission }, configParameters: config);

                bool minerADisconnectedFromMinerB = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromMinerB)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tips has rewound to 10.
                        TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, minerB);
                        minerADisconnectedFromMinerB = true;
                    }
                }

                // Start the nodes.
                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB, syncer);

                // Start minerA and mine 10 blocks. We cannot use a premade chain as it adversely affects the max tip age calculation, causing sporadic sync errors.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                minerA.SetDisconnectInterceptor(interceptor);

                // MinerB/Syncer syncs with MinerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disable block propagation from MinerA to MinerB so that it can mine on its own and create a fork.
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // MinerA continues to mine to height 14.
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // MinerB mines 5 more blocks so that a reorg is triggered.
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));

                // MinerA and Syncer should have reorged to the longer chain.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
            }
        }
    }
}
