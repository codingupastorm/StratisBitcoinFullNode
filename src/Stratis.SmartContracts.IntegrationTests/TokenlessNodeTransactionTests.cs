using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.Features.PoA;
using Stratis.Features.PoA.Tests.Common;
using Stratis.Features.PoA.Voting;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeTransactionTests
    {
        private readonly TokenlessNetwork network;

        public TokenlessNodeTransactionTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public async Task TokenlessNodesMineAnEmptyBlockAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.
                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
            }
        }

        [Fact]
        public async Task TokenlessNodesConnectAndMineOpReturnAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.
                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node1);
                await node1.BroadcastTransactionAsync(transaction);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // Other node receives and mines transaction, validating it came from a permitted sender.
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                var block = node1.FullNode.ChainIndexer.GetHeader(1).Block;
                Assert.Equal(2, block.Transactions.Count);
            }
        }

        [Fact]
        public async Task TokenlessNodesConnectAndMineWithoutPasswordAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.
                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                // Stop the nodes.
                node1.FullNode.Dispose();
                node2.FullNode.Dispose();

                // Now start the nodes without passwords.
                node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1, initialRun: false);
                node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2, initialRun: false);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node1);
                await node1.BroadcastTransactionAsync(transaction);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // Other node receives and mines transaction, validating it came from a permitted sender.
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                var block = node1.FullNode.ChainIndexer.GetHeader(1).Block;
                Assert.Equal(2, block.Transactions.Count);
            }
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallAContractAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey);

                await node1.BroadcastTransactionAsync(callTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt callReceipt = receiptRepository.Retrieve(callTransaction.GetHash());
                Assert.True(callReceipt.Success);
            }
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallWithControllerAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var node1Controller = node1.FullNode.NodeController<TokenlessController>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");

                var createModel = new BuildCreateContractTransactionModel()
                {
                    ContractCode = compilationResult.Compilation
                };

                var createResult = (JsonResult)node1Controller.BuildCreateContractTransaction(createModel);
                var createResponse = (BuildCreateContractTransactionResponse)createResult.Value;

                await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = createResponse.Hex
                });

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createResponse.TransactionId);
                Assert.True(createReceipt.Success);

                var callModel = new BuildCallContractTransactionModel()
                {
                    Address = createReceipt.NewContractAddress.ToBase58Address(this.network),
                    MethodName = "CallMe"
                };

                var callResult = (JsonResult)node1Controller.BuildCallContractTransaction(callModel);
                var callResponse = (BuildCallContractTransactionResponse)callResult.Value;

                await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = callResponse.Hex
                });

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt callReceipt = receiptRepository.Retrieve(callResponse.TransactionId);
                Assert.True(callReceipt.Success);

                // Also check that OpReturn Transaction can be sent via controller
                var opReturnModel = new BuildOpReturnTransactionModel()
                {
                    OpReturnData = "Sending a message via opReturn 123"
                };

                var opReturnResult = (JsonResult)node1Controller.BuildOpReturnTransaction(opReturnModel);
                var opReturnResponse = (TokenlessTransactionModel)opReturnResult.Value;

                await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = opReturnResponse.Hex
                });

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
            }
        }

        [Fact]
        public async Task TokenlessNodesKickAMinerBasedOnCAAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Start the network with only 2 certificates generated.
                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // As the network had 3 federation members on startup, one should be voted out. Wait for a scheduled vote.
                VotingManager node1VotingManager = node1.FullNode.NodeService<VotingManager>();
                VotingManager node2VotingManager = node2.FullNode.NodeService<VotingManager>();
                TestBase.WaitLoop(() => node1VotingManager.GetScheduledVotes().Count > 0);
                TestBase.WaitLoop(() => node2VotingManager.GetScheduledVotes().Count > 0);

                // Mine some blocks to lock in the vote
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                List<Poll> finishedPolls = node1VotingManager.GetFinishedPolls();
                Assert.Single(finishedPolls);
                Assert.Equal(VoteKey.KickFederationMember, finishedPolls.First().VotingData.Key);

                // Mine some more blocks to execute the vote and reduce number of federation members to 2.
                await node1.MineBlocksAsync(5);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                await node2.MineBlocksAsync(5);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Ensure we have only 2 federation members now.
                IFederationManager node1FederationManager = node1.FullNode.NodeService<IFederationManager>();
                Assert.Equal(2, node1FederationManager.GetFederationMembers().Count);

                // Mine blocks based on a 2-slot federation to evoke possible bans due to incorrect slot resolution.
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                await node2.MineBlocksAsync(2);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Last of all, create a 3rd node and check that nobody gets banned.
                CaClient client3 = TokenlessTestHelper.GetClient(server);
                CoreNode node3 = nodeBuilder.CreateTokenlessNode(this.network, 2, ac, client3);

                certificates.Add(node3.ClientCertificate.ToCertificate());

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node3.DataFolder, this.network.RootFolderName, this.network.Name));

                node3.Start();

                TestHelper.ConnectNoCheck(node3, node2);
                TestHelper.ConnectNoCheck(node3, node1);

                var addressManagers = new[] {
                    node1.FullNode.NodeService<IPeerAddressManager>(),
                    node2.FullNode.NodeService<IPeerAddressManager>(),
                    node3.FullNode.NodeService<IPeerAddressManager>()
                };

                bool HaveBans()
                {
                    return addressManagers.Any(a => a.Peers.Any(p => !string.IsNullOrEmpty(p.BanReason)));
                }

                TestBase.WaitLoop(() => HaveBans() || (TestHelper.IsNodeConnectedTo(node3, node2) && TestHelper.IsNodeConnectedTo(node3, node1)));

                // See if 3rd node gets voted in.
                TestBase.WaitLoop(() => HaveBans() || (node1VotingManager.GetScheduledVotes().Count > 0 && node2VotingManager.GetScheduledVotes().Count > 0));

                Assert.False(HaveBans(), "Some node(s) got banned");

                // Mine some blocks to lock in the vote
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                finishedPolls = node1VotingManager.GetFinishedPolls();
                Assert.Equal(2, finishedPolls.Count);
                Assert.Equal(VoteKey.AddFederationMember, finishedPolls[1].VotingData.Key);

                // Mine some more blocks to execute the vote and add a 3rd federation member
                await node1.MineBlocksAsync(5);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                await node2.MineBlocksAsync(5);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Ensure we have 3 federation members now.
                Assert.Equal(3, node1FederationManager.GetFederationMembers().Count);

                // And lastly, that our 3rd guy can now mine.
                await node3.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);
            }
        }

        [Fact]
        public async Task TokenlessNodesCanSendSameOpReturnDataTwiceAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var node1Controller = node1.FullNode.NodeController<TokenlessController>();

                var opReturnModel = new BuildOpReturnTransactionModel()
                {
                    OpReturnData = "0203040509"
                };

                var opReturnResult = (JsonResult)node1Controller.BuildOpReturnTransaction(opReturnModel);
                var opReturnResponse = (TokenlessTransactionModel)opReturnResult.Value;
                var transactionId1 = opReturnResponse.TransactionId;
                var tx1 = this.network.Consensus.ConsensusFactory.CreateTransaction(opReturnResponse.Hex);

                await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = opReturnResponse.Hex
                });

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Build and send the same transaction again.
                opReturnResult = (JsonResult)node1Controller.BuildOpReturnTransaction(opReturnModel);
                opReturnResponse = (TokenlessTransactionModel)opReturnResult.Value;

                var transactionId2 = opReturnResponse.TransactionId;
                var tx2 = this.network.Consensus.ConsensusFactory.CreateTransaction(opReturnResponse.Hex);

                var result = await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = opReturnResponse.Hex
                });

                var sendTransactionResult = (JsonResult)result;
                var sendTransactionResponse = (Features.MemoryPool.Broadcasting.SendTransactionModel)sendTransactionResult.Value;

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Confirm that the tx was mined.
                Assert.Contains(node1.GetTip().Block.Transactions, t => t.GetHash() == transactionId2);
                Assert.NotEqual(transactionId1, transactionId2);
                Assert.NotEqual(tx1.Time, tx2.Time);
            }
        }

        [Fact(Skip = "This is useful locally to test the Swagger UI for Pure methods.")]
        public async Task SwaggerRendersPureMethods()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var node1Controller = node1.FullNode.NodeController<TokenlessController>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessPureExample.cs");

                var createModel = new BuildCreateContractTransactionModel()
                {
                    ContractCode = compilationResult.Compilation
                };

                var createResult = (JsonResult)node1Controller.BuildCreateContractTransaction(createModel);
                var createResponse = (BuildCreateContractTransactionResponse)createResult.Value;

                await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = createResponse.Hex
                });

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createResponse.TransactionId);
                Assert.True(createReceipt.Success);
            }
        }
    }
}
