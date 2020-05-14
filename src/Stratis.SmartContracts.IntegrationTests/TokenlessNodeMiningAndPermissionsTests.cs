﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA.Voting;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class TokenlessNodeMiningAndPermissionsTests
    {
        private readonly TokenlessNetwork network;

        public TokenlessNodeMiningAndPermissionsTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public async Task TokenlessNodeDoesNotHaveMiningPermissionDoesNotMineAsync()
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
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission });

                node1.Start();

                // Try and mine 2 blocks
                await node1.MineBlocksAsync(2);

                // The height should not have increased.
                Assert.Equal(0, node1.FullNode.ConsensusManager().Tip.Height);
            }
        }

        [Fact]
        public async Task NodeGetsCertificateRevokedCannotPropagateTransactionsAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var caAdminClient = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(caAdminClient.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                node1.Start();
                node2.Start();

                // Connect the 2 nodes.
                TestHelper.Connect(node1, node2);

                // Revoke node 2's certificate.
                Assert.True(caAdminClient.RevokeCertificate(node2.ClientCertificate.Thumbprint));

                // Create a transaction on node2 and try and propagate it.
                var transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node2);
                await node2.BroadcastTransactionAsync(transaction);

                // Mine a block on node 1 and check that the transaction was never received and/or included.
                await node1.MineBlocksAsync(1);

                var block = node1.FullNode.BlockStore().GetBlock(node1.FullNode.ConsensusManager().Tip.HashBlock);
                Assert.DoesNotContain(transaction.GetHash(), block.Transactions.Select(t => t.GetHash()));
            }
        }

        [Fact]
        public async Task AddedNodeCanMineWithoutBreakingAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);
                CoreNode node3 = nodeBuilder.CreateTokenlessNode(this.network, 2, server);

                // Get them connected and mining
                node1.Start();
                node2.Start();
                node3.Start();
                TestHelper.Connect(node1, node2);
                TestHelper.Connect(node1, node3);
                TestHelper.Connect(node2, node3);

                await node1.MineBlocksAsync(3);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);

                // Now add a 4th
                CoreNode node4 = nodeBuilder.CreateTokenlessNode(this.network, 3, server);
                node4.Start();

                VotingManager node1VotingManager = node1.FullNode.NodeService<VotingManager>();
                VotingManager node2VotingManager = node2.FullNode.NodeService<VotingManager>();
                VotingManager node3VotingManager = node2.FullNode.NodeService<VotingManager>();
                TestBase.WaitLoop(() => node1VotingManager.GetScheduledVotes().Count > 0);
                TestBase.WaitLoop(() => node2VotingManager.GetScheduledVotes().Count > 0);
                TestBase.WaitLoop(() => node3VotingManager.GetScheduledVotes().Count > 0);

                // Mine some blocks to lock in the vote
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);
                await node3.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);

                // Mine some more blocks to execute the vote and increase federation members.
                await node1.MineBlocksAsync(5);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);
                await node2.MineBlocksAsync(4);

                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);

                TestHelper.Connect(node1, node4);
                TokenlessTestHelper.WaitForNodeToSync(node1, node4);

                // Finally, see if node4 can mine fine.
                await node4.MineBlocksAsync(1);
            }
        }

        [Fact]
        public async Task RestartTokenlessNodeAfterBlocksMinedAndContinuesAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);

                node1.Start();

                // Mine 20 blocks
                await node1.MineBlocksAsync(20);
                TestHelper.IsNodeSyncedAtHeight(node1, 20);

                // Restart the node and ensure that it is still at height 20.
                node1.Restart();
                TestHelper.IsNodeSyncedAtHeight(node1, 20);
            }
        }
    }
}
