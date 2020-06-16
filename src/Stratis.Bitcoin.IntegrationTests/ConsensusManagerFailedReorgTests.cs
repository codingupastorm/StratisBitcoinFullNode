using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Threading.Tasks;
using CertificateAuthority.Tests.Common;
using CertificateAuthority;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Consensus.Rules;
using Stratis.Core.Primitives;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA;
using Stratis.SmartContracts.Tests.Common;
using Xunit;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.IntegrationTests
{
    /// <summary>
    /// This rule allows us to set up a block that fails when full validation is performed 
    /// by simply providing an empty signature.
    /// </summary>
    public sealed class FullValidationSignatureRule : FullValidationConsensusRule
    {
        public FullValidationSignatureRule() : base()
        {
        }

        public override Task RunAsync(RuleContext context)
        {
            if ((context.ValidationContext.BlockToValidate.Header as PoABlockHeader).BlockSignature.Signature.Length == 0)
            {
                this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
                PoAConsensusErrors.InvalidBlockSignature.Throw();
            }

            return Task.CompletedTask;
        }
    }
    /// <summary>
    /// This rule allows us to set up a block that fails when partial validation is performed 
    /// by simply providing an empty signature.
    /// </summary>
    public sealed class PartialValidationSignatureRule : PartialValidationConsensusRule
    {
        public PartialValidationSignatureRule() : base()
        {
        }

        public override Task RunAsync(RuleContext context)
        {
            if ((context.ValidationContext.BlockToValidate.Header as PoABlockHeader).BlockSignature.Signature.Length == 0)
            {
                this.Logger.LogTrace("(-)[INVALID_SIGNATURE]");
                PoAConsensusErrors.InvalidBlockSignature.Throw();
            }

            return Task.CompletedTask;
        }
    }


    public class ConsensusManagerFailedReorgTests
    {
        private static List<string> FederationPermissions = new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission };

        public ConsensusManagerFailedReorgTests()
        {
        }

        [Fact]
        public async Task Reorg_FailsFV_Reconnect_OldChain_ConnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-1-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 1, server, agent: "cmfr-1-minerB", permissions: FederationPermissions).NoValidation();

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.FullValidationRules.Add(typeof(FullValidationSignatureRule));

                ChainedHeader minerBChainTip = null;
                bool interceptorsEnabled = false;
                bool minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = false;
                bool minerA_IsConnecting_To_MinerBChain = false;
                bool minerA_Disconnected_MinerBsChain = false;
                bool minerA_Reconnected_Its_OwnChain = false;

                // Configure the interceptor to intercept when Miner A connects Miner B's chain.
                void interceptorConnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!interceptorsEnabled)
                        return;

                    if (!minerA_IsConnecting_To_MinerBChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 12)
                            minerA_IsConnecting_To_MinerBChain = minerA.FullNode.ConsensusManager().Tip.HashBlock == minerBChainTip.GetAncestor(12).HashBlock;

                        return;
                    }

                    if (!minerA_Reconnected_Its_OwnChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 14)
                            minerA_Reconnected_Its_OwnChain = true;

                        return;
                    }
                }

                // Configure the interceptor to intercept when Miner A disconnects Miner B's chain after the reorg.
                void interceptorDisconnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!interceptorsEnabled)
                        return;

                    if (!minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = true;

                        return;
                    }

                    if (!minerA_Disconnected_MinerBsChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_MinerBsChain = true;

                        return;
                    }
                }

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB);

                // Mine initial blocks.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                minerA.SetConnectInterceptor(interceptorConnect);
                minerA.SetDisconnectInterceptor(interceptorDisconnect);

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);

                // Disable Miner A from sending blocks to Miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14.
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // Confirm that minerB is still at height 10.
                Assert.Equal(10, minerB.FullNode.ConsensusManager().Tip.Height);

                // Enable the interceptors so that they are active during the reorg.
                interceptorsEnabled = true;

                // Miner B mines 5 more blocks:
                // Block 11,12,14,15 = valid
                // Block 13 = invalid
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerB.FullNode.ChainIndexer.GetHeader(13).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                minerBChainTip = minerB.FullNode.ConsensusManager().Tip;
                Assert.Equal(15, minerBChainTip.Height);

                TestHelper.EnableBlockPropagation(minerA, minerB);

                minerB.MineBlocksAsync(1).GetAwaiter().GetResult();

                // Wait until Miner A disconnected its own chain so that it can connect to
                // Miner B's longer chain.
                TestBase.WaitLoop(() => minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain);

                // Wait until Miner A has connected Miner B's chain (but failed)
                TestBase.WaitLoop(() => minerA_IsConnecting_To_MinerBChain);

                // Wait until Miner A has disconnected Miner B's invalid chain.
                TestBase.WaitLoop(() => minerA_Disconnected_MinerBsChain);

                // Wait until Miner A has reconnected its own chain.
                TestBase.WaitLoop(() => minerA_Reconnected_Its_OwnChain);
            }
        }

        [Fact]
        public async Task Reorg_FailsFV_Reconnect_OldChain_Nodes_DisconnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-2-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 1, server, agent: "cmfr-2-minerB", permissions: FederationPermissions).NoValidation();

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.FullValidationRules.Add(typeof(FullValidationSignatureRule));

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB);

                // Mine initial blocks.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable Miner A from sending blocks to Miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // Confirm that minerB is still at height 10.
                Assert.Equal(10, minerB.FullNode.ConsensusManager().Tip.Height);

                // Disable Miner B from sending blocks to miner A
                TestHelper.DisableBlockPropagation(minerB, minerA);

                // Miner B mines 5 more blocks:
                // Block 11,12,14,15 = valid
                // Block 13 = invalid
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerB.FullNode.ChainIndexer.GetHeader(13).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                var minerBChainTip = minerB.FullNode.ConsensusManager().Tip;
                Assert.Equal(15, minerBChainTip.Height);

                TestHelper.EnableBlockPropagation(minerB, minerA);

                bool minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = false;
                bool minerA_IsConnecting_To_MinerBChain = false;
                bool minerA_Disconnected_MinerBsChain = false;
                bool minerA_Reconnected_Its_OwnChain = false;

                // Configure the interceptor to intercept when Miner A connects Miner B's chain.
                void interceptorConnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!minerA_IsConnecting_To_MinerBChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 12)
                            minerA_IsConnecting_To_MinerBChain = minerA.FullNode.ConsensusManager().Tip.HashBlock == minerBChainTip.GetAncestor(12).HashBlock;

                        return;
                    }

                    if (!minerA_Reconnected_Its_OwnChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 14)
                            minerA_Reconnected_Its_OwnChain = true;

                        return;
                    }
                }

                // Configure the interceptor to intercept when Miner A disconnects Miner B's chain after the reorg.
                void interceptorDisconnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = true;

                        return;
                    }
                    else

                    if (!minerA_Disconnected_MinerBsChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_MinerBsChain = true;

                        return;
                    }
                }

                minerA.Restart();
                minerA.SetConnectInterceptor(interceptorConnect);
                minerA.SetDisconnectInterceptor(interceptorDisconnect);

                TestHelper.ConnectNoCheck(minerA, minerB);

                // Wait until Miner A disconnected its own chain so that it can connect to
                // Miner B's longer chain.
                TestBase.WaitLoop(() => minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain);

                // Wait until Miner A has connected Miner B's chain (but failed)
                TestBase.WaitLoop(() => minerA_IsConnecting_To_MinerBChain);

                // Wait until Miner A has disconnected Miner B's invalid chain.
                TestBase.WaitLoop(() => minerA_Disconnected_MinerBsChain);

                // Wait until Miner A has reconnected its own chain.
                TestBase.WaitLoop(() => minerA_Reconnected_Its_OwnChain);
            }
        }

        [Fact]
        public async Task Reorg_FailsFV_Reconnect_OldChain_From2ndMiner_DisconnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-3-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 1, server, agent: "cmfr-3-minerB", permissions: FederationPermissions).NoValidation();
                CoreNode syncer = nodeBuilder.CreateTokenlessNode(network, 2, server, agent: "cmfr-3-syncer", permissions: FederationPermissions);

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.FullValidationRules.Add(typeof(FullValidationSignatureRule));

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB, syncer);

                // MinerA mines 5 blocks
                minerA.MineBlocksAsync(5).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 5);

                // MinerB and Syncer syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);
                TestHelper.ConnectAndSync(syncer, minerA);

                // Disconnect minerB from miner A
                TestHelper.Disconnect(minerB, minerA);
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(minerB));

                // Miner A continues to mine to height 9
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 5);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);

                // Disconnect syncer from minerA
                TestHelper.Disconnect(syncer, minerA);
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnected(minerA));

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerB.FullNode.ChainIndexer.GetHeader(8).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                var minerBChainTip = minerB.FullNode.ConsensusManager().Tip;
                Assert.Equal(10, minerBChainTip.Height);

                // Reconnect syncer to minerB causing the following to happen:
                // Reorg from blocks 9 to 5.
                // Connect blocks 5 to 10
                // Block 8 fails.
                // Reorg from 7 to 5
                // Reconnect blocks 6 to 9
                TestHelper.ConnectNoCheck(syncer, minerB);

                TestHelper.AreNodesSynced(minerA, syncer);

                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);
            }
        }

        [Fact]
        public async Task Reorg_FailsPartialValidation_ConnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-4-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 1, server, agent: "cmfr-4-minerB", permissions: FederationPermissions).NoValidation();

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.PartialValidationRules.Add(typeof(PartialValidationSignatureRule));

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB);

                // MinerA mines 10 blocks.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // Miner B syncs with Miner A.
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable Miner A from sending blocks to Miner B.
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14.
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // MinerB mines 5 more blocks:
                // Block 11,12,13,15 = valid
                // Block 14 = invalid
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerB.FullNode.ChainIndexer.GetHeader(14).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                var minerBChainTip = minerB.FullNode.ConsensusManager().Tip;
                Assert.Equal(15, minerBChainTip.Height);

                // MinerA will disconnect MinerB
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Ensure Miner A and Miner B remains on their respective heights.
                var badBlockOnMinerBChain = minerBChainTip.GetAncestor(14);
                Assert.Null(minerA.FullNode.ConsensusManager().Tip.FindAncestorOrSelf(badBlockOnMinerBChain));
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 15);
            }
        }

        [Fact]
        public async Task Reorg_FailsPartialValidation_DisconnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-5-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 1, server, agent: "cmfr-5-minerB", permissions: FederationPermissions).NoValidation();

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.PartialValidationRules.Add(typeof(PartialValidationSignatureRule));

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB);

                // MinerA mines 10 blocks.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disconnect Miner A from Miner B
                TestHelper.Disconnect(minerB, minerA);

                // Miner A continues to mine to height 14
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // MinerB mines 5 more blocks:
                // Block 11,12,13,15 = valid
                // Block 14 = invalid
                minerB.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerB.FullNode.ChainIndexer.GetHeader(14).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                var minerBChainTip = minerB.FullNode.ConsensusManager().Tip;
                Assert.Equal(15, minerBChainTip.Height);

                // Reconnect Miner A to Miner B.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // Miner A will disconnect Miner B
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Ensure Miner A and Miner B remains on their respective heights.
                var badBlockOnMinerBChain = minerBChainTip.GetAncestor(14);
                Assert.Null(minerA.FullNode.ConsensusManager().Tip.FindAncestorOrSelf(badBlockOnMinerBChain));
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 15);

            }
        }

        /// <summary>
        /// The chain that will be reconnected to has 4 blocks and 4 headers from fork point:
        ///
        /// 6 -> Full Block
        /// 7 -> Full Block
        /// 8 -> Full Block
        /// 9 -> Full Block
        /// 10 -> Header Only
        /// 11 -> Header Only
        /// 12 -> Header Only
        /// 13 -> Header Only
        /// </summary>
        [Fact]
        public async Task Reorg_FailsFV_ChainHasBlocksAndHeadersOnly_DisconnectedAsync()
        {
            var network = new TokenlessNetwork();
            var noValidationRulesNetwork = new TokenlessNetwork() { Name = "NoValidationRulesNetwork" };
            noValidationRulesNetwork.Consensus.MaxReorgLength = 100;

            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.
                CoreNode minerA = nodeBuilder.CreateTokenlessNode(network, 0, server, agent: "cmfr-6-minerA", permissions: FederationPermissions);
                CoreNode minerB = nodeBuilder.CreateTokenlessNode(network, 1, server, agent: "cmfr-6-minerB", permissions: FederationPermissions);
                CoreNode minerC = nodeBuilder.CreateTokenlessNode(noValidationRulesNetwork, 2, server, agent: "cmfr-6-minerC", permissions: FederationPermissions).NoValidation();

                // We are only interested in failing a specific block.
                var minerARules = network.Consensus.ConsensusRules;
                minerARules.HeaderValidationRules.Clear();
                minerARules.IntegrityValidationRules.Clear();
                minerARules.PartialValidationRules.Clear();
                minerARules.FullValidationRules.Clear();
                minerARules.FullValidationRules.Add(typeof(FullValidationSignatureRule));

                TokenlessTestHelper.ShareCertificatesAndStart(network, minerA, minerB, minerC);

                // MinerA mines 10 blocks.
                minerA.MineBlocksAsync(10).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // MinerB and MinerC syncs with MinerA
                TestHelper.ConnectAndSync(minerA, minerB, minerC);

                // Disconnect MinerC from MinerA
                TestHelper.Disconnect(minerA, minerC);

                // MinerA continues to mine to height 14
                minerA.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 14, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestBase.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 14, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestBase.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 10, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });

                // MinerB continues to mine to block 18 without block propogation
                TestHelper.DisableBlockPropagation(minerB, minerA);
                minerB.MineBlocksAsync(4).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 14));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 18));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerC, 10));

                // MinerC mines 5 more blocks:
                // Block 11,12,14,15 = valid
                // Block 13 = invalid
                minerC.MineBlocksAsync(5).GetAwaiter().GetResult();
                ((minerC.FullNode.ChainIndexer.GetHeader(13).Block as Block).Header as PoABlockHeader).BlockSignature = new BlockSignature();
                var minerCChainTip = minerC.FullNode.ConsensusManager().Tip;
                Assert.Equal(15, minerCChainTip.Height);

                // Reconnect MinerA to MinerC.
                TestHelper.ConnectNoCheck(minerA, minerC);

                // MinerC should be disconnected from MinerA
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerC));

                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestBase.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 14, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestBase.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 18, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestBase.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 15, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });
            }
        }
    }
}