using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Core.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;
using System.Security;
using Stratis.Feature.PoA.Tokenless.Networks;
using Microsoft.AspNetCore.Hosting;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using CertificateAuthority;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Consensus;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public class MemoryPoolTests
    {
        private const string Password = "password";
        private const string WalletName = "mywallet";
        private const string Passphrase = "passphrase";
        private const string Account = "account 0";

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
               
                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();

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

                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();

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

                CoreNode stratisNodeSync = nodeBuilder.CreateTokenlessNode(this.network, 0, server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
                CoreNode stratisNode1 = nodeBuilder.CreateTokenlessNode(this.network, 1, server, permissions: new List<string>() { CaCertificatesManager.SendPermission });
                CoreNode stratisNode2 = nodeBuilder.CreateTokenlessNode(this.network, 2, server, permissions: new List<string>() { CaCertificatesManager.SendPermission });

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

        // TODO: Not relevant for tokenless networks?
        /*         
        [Fact]
        public void Mempool_SendPosTransaction_AheadOfFutureDrift_ShouldRejectByMempool()
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
                CoreNode stratisSender = nodeBuilder.CreateTokenlessNode(this.network, 0, server).Start();

                stratisSender.MineBlocksAsync(5).GetAwaiter().GetResult();

                // Build a transaction.
                Transaction trx = TokenlessTestHelper.CreateBasicOpReturnTransaction(stratisSender);

                // This should make the mempool reject a POS trx.
                trx.Time = Utils.DateTimeToUnixTime(Utils.UnixTimeToDateTime(trx.Time).AddMinutes(5));

                // Sign trx again after changing the time property.
                ITokenlessSigner signer = stratisSender.FullNode.NodeService<ITokenlessSigner>();
                trx.Inputs.Clear();
                signer.InsertSignedTxIn(trx, stratisSender.TransactionSigningPrivateKey.GetBitcoinSecret(TokenlessTestHelper.Network));

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal("time-too-new", entry.ErrorMessage);
            }
        }
        */

        // TODO: Not relevant for tokenless networks?
        /*
        [Fact]
        public void Mempool_SendPosTransaction_WithElapsedLockTime_ShouldBeAcceptedByMempool()
        {
            // See CheckFinalTransaction_WithElapsedLockTime_ReturnsTrueAsync for the 'unit test' version

            var network = new StratisRegTest();

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPosNode(network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest10Miner).Start();

                TestHelper.MineBlocks(stratisSender, 5);

                // Send coins to the receiver.
                var context = CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);

                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(context);

                // Treat the locktime as absolute, not relative.
                trx.Inputs.First().Sequence = new Sequence(Sequence.SEQUENCE_LOCKTIME_DISABLE_FLAG);

                // Set the nLockTime to be behind the current tip so that locktime has elapsed.
                trx.LockTime = new LockTime(stratisSender.FullNode.ChainIndexer.Height - 1);

                // Sign trx again after changing the nLockTime.
                trx = context.TransactionBuilder.SignTransaction(trx);

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx);

                TestBase.WaitLoop(() => stratisSender.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count == 1);
            }
        }
        */

        // TODO: Not relevant for tokenless networks?
        /*
        [Fact]
        public void Mempool_SendPosTransaction_WithFutureLockTime_ShouldBeRejectedByMempool()
        {
            // See AcceptToMemoryPool_TxFinalCannotMine_ReturnsFalseAsync for the 'unit test' version

            var network = new StratisRegTest();

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPosNode(network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest10Miner).Start();

                TestHelper.MineBlocks(stratisSender, 5);

                // Send coins to the receiver.
                var context = CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);

                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(context);

                // Treat the locktime as absolute, not relative.
                trx.Inputs.First().Sequence = new Sequence(Sequence.SEQUENCE_LOCKTIME_DISABLE_FLAG);

                // Set the nLockTime to be ahead of the current tip so that locktime has not elapsed.
                trx.LockTime = new LockTime(stratisSender.FullNode.ChainIndexer.Height + 1);

                // Sign trx again after changing the nLockTime.
                trx = context.TransactionBuilder.SignTransaction(trx);

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal("non-final", entry.ErrorMessage);
            }
        }
        */

        [Fact]
        public void Mempool_SendOversizeTransaction_ShouldRejectByMempool()
        {
            var network = new StratisRegTest();

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPosNode(network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest10Miner).Start();

                TestHelper.MineBlocks(stratisSender, 5);

                // Send coins to the receiver
                var context = CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);

                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(context);

                // Add nonsense script to make tx large.
                Script script = Script.FromBytesUnsafe(new string('A', network.Consensus.Options.MaxStandardTxWeight).Select(c => (byte)c).ToArray());
                trx.Outputs.Add(new TxOut(new Money(1), script));

                // Sign trx again after adding an output
                trx = context.TransactionBuilder.SignTransaction(trx);

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal("tx-size", entry.ErrorMessage);
            }
        }

        [Fact]
        public void Mempool_SendTransactionWithEarlyTimestamp_ShouldRejectByMempool()
        {
            var network = new StratisRegTest();

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPosNode(network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest10Miner).Start();

                TestHelper.MineBlocks(stratisSender, 5);

                // Send coins to the receiver
                var context = CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);

                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(context);

                // Use timestamp value that is definitely earlier than the input's timestamp
                trx.Time = 1;

                // Sign trx again after mutating timestamp
                trx = context.TransactionBuilder.SignTransaction(trx);

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal("timestamp earlier than input", entry.ErrorMessage);
            }
        }

        // TODO: There is no need for this to be a full integration test, there just needs to be a PoS version of the test chain used in the validator unit tests
        [Fact]
        public void Mempool_SendTransactionWithLargeOpReturn_ShouldRejectByMempool()
        {
            var network = new StratisRegTest();

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPosNode(network).WithReadyBlockchainData(ReadyBlockchain.StratisRegTest10Miner).Start();

                TestHelper.MineBlocks(stratisSender, 5);

                // Send coins to the receiver.
                var context = CreateContext(network, new WalletAccountReference(WalletName, Account), Password, new Key().PubKey.GetAddress(network).ScriptPubKey, Money.COIN * 100, FeeType.Medium, 1);
                context.OpReturnData = "1";
                context.OpReturnAmount = Money.Coins(0.01m);
                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(context);

                foreach (TxOut output in trx.Outputs)
                {
                    if (output.ScriptPubKey.IsUnspendable)
                    {
                        int[] data =
                        {
                            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                        };
                        var ops = new Op[data.Length + 1];
                        ops[0] = OpcodeType.OP_RETURN;
                        for (int i = 0; i < data.Length; i++)
                        {
                            ops[1 + i] = Op.GetPushOp(data[i]);
                        }

                        output.ScriptPubKey = new Script(ops);
                    }
                }

                // Sign trx again after lengthening nulldata output.
                trx = context.TransactionBuilder.SignTransaction(trx);

                // Enable standard policy relay.
                stratisSender.FullNode.NodeService<MempoolSettings>().RequireStandard = true;

                var broadcaster = stratisSender.FullNode.NodeService<IBroadcasterManager>();

                broadcaster.BroadcastTransactionAsync(trx).GetAwaiter().GetResult();
                var entry = broadcaster.GetTransaction(trx.GetHash());

                Assert.Equal("scriptpubkey", entry.ErrorMessage);
            }
        }

        public static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }
    }
}