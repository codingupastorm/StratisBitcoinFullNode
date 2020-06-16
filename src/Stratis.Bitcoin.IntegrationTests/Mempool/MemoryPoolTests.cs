using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;
using Stratis.Feature.PoA.Tokenless.Networks;
using Microsoft.AspNetCore.Hosting;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using CertificateAuthority;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Org.BouncyCastle.X509;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public class MemoryPoolTests
    {
        private static List<string> FederationPermissions = new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission };
        private readonly TokenlessNetwork network;

        public MemoryPoolTests()
        {
            this.network = new TokenlessNetwork();
        }

        [Fact]
        public void AddToMempool()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));
               
                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: FederationPermissions).Start();

                Transaction tx = TokenlessTestHelper.CreateBasicOpReturnTransaction(stratisNodeSync);

                stratisNodeSync.BroadcastTransactionAsync(tx).GetAwaiter().GetResult();

                TestBase.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 1);
            }
        }

        [Fact]
        public void MempoolReceiveFromManyNodes()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: FederationPermissions).Start();

                var trxs = new List<Transaction>();
                var rand = new Random();
                foreach (int index in Enumerable.Range(1, 30))
                {
                    var data = new byte[4];
                    rand.NextBytes(data);
                    Transaction tx = TokenlessTestHelper.CreateBasicOpReturnTransaction(stratisNodeSync, data);
                    trxs.Add(tx);
                }

                var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    stratisNodeSync.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();
                });

                TestBase.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 30);
            }
        }

        [Fact]
        public void MempoolSyncTransactions()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: FederationPermissions).Start();
                CoreNode stratisNode1 = nodeBuilder.CreateTokenlessNode(this.network, 1, server, permissions: FederationPermissions);
                CoreNode stratisNode2 = nodeBuilder.CreateTokenlessNode(this.network, 2, server, permissions: FederationPermissions);

                var certificates = new List<X509Certificate>() { stratisNodeSync.ClientCertificate.ToCertificate()};
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, stratisNode1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, stratisNode2.DataFolder, this.network);

                stratisNode1.Start();
                stratisNode2.Start();

                // Sync both nodes.
                TestHelper.ConnectAndSync(stratisNode1, stratisNodeSync);
                TestHelper.ConnectAndSync(stratisNode2, stratisNodeSync);

                // Create some transactions and push them to the pool.
                var trxs = new List<Transaction>();
                var rand = new Random();
                foreach (int index in Enumerable.Range(1, 5))
                {
                    var data = new byte[4];
                    rand.NextBytes(data);
                    Transaction tx = TokenlessTestHelper.CreateBasicOpReturnTransaction(stratisNodeSync, data);
                    trxs.Add(tx);
                }

                var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    stratisNodeSync.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();
                });

                // wait for all nodes to have all trx
                TestBase.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 5);

                // the full node should be connected to both nodes
                Assert.True(stratisNodeSync.FullNode.ConnectionManager.ConnectedPeers.Count() >= 2);

                TestBase.WaitLoop(() => stratisNode1.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 5);
                TestBase.WaitLoop(() => stratisNode2.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 5);

                // mine the transactions in the mempool
                stratisNodeSync.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 0);

                // wait for block and mempool to change
                TestBase.WaitLoop(() => stratisNode1.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock);
                TestBase.WaitLoop(() => stratisNode2.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock);
                TestBase.WaitLoop(() => stratisNode1.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 0);
                TestBase.WaitLoop(() => stratisNode2.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 0);
            }
        }

        [Fact]
        public void MineBlocksBlockOrphanedAfterReorgTxsReturnedToMempool()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Setup two synced nodes with some mined blocks.
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server).Start();

                var mempoolValidationState = new MempoolValidationState(true);

                node1.MineBlocksAsync(20).GetAwaiter().GetResult();
                TestHelper.ConnectAndSync(node1, node2);

                // Nodes disconnect.
                TestHelper.Disconnect(node1, node2);

                // Create tx and node 1 has this in mempool.
                Transaction transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node1);

                Assert.True(node1.FullNode.MempoolManager().Validator.AcceptToMemoryPool(mempoolValidationState, transaction).Result);
                Assert.Contains(transaction.GetHash(), node1.FullNode.MempoolManager().GetMempoolAsync().Result);

                // Node 2 has none in its mempool.
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().MempoolSize().Result == 0);

                // Node 1 mines new tx into block - removed from mempool.
                node1.MineBlocksAsync(1).GetAwaiter().GetResult();
                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().MempoolSize().Result == 0);
                uint256 minedBlockHash = node1.FullNode.ChainIndexer.Tip.HashBlock;

                // Node 2 mines two blocks to have greatest chainwork.
                node2.MineBlocksAsync(2).GetAwaiter().GetResult();

                // Sync nodes and reorg occurs.
                TestHelper.ConnectAndSync(node1, true, node2);

                // Block mined by Node 1 is orphaned.
                Assert.Null(node1.FullNode.ChainBehaviorState.ConsensusTip.FindAncestorOrSelf(minedBlockHash));

                // Tx is returned to mempool.
                Assert.Contains(transaction.GetHash(), node1.FullNode.MempoolManager().GetMempoolAsync().Result);

                // New mined block contains this transaction from the orphaned block.
                node1.MineBlocksAsync(1).GetAwaiter().GetResult();
                Assert.Contains(transaction, node1.FullNode.ChainIndexer.Tip.Block.Transactions);
            }
        }
    }
}