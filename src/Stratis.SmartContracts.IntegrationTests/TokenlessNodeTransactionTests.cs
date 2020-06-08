using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.P2P;
using Stratis.Core.Utilities.JsonErrors;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA;
using Stratis.Features.PoA.Voting;
using Stratis.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeTransactionTests
    {
        private readonly TokenlessNetwork network;

        public TokenlessNodeTransactionTests() : base()
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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
            }
        }

        [Fact]
        public async Task TokenlessSystemChannelNodesMineAnEmptyBlockAsync()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateInfraNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateInfraNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

                node1.Start();
                node2.Start();

                // Stop the nodes.
                node1.FullNode.Dispose();
                node2.FullNode.Dispose();

                // Now start the nodes without passwords.
                node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server, initialRun: false);
                node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server, initialRun: false);

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/TokenlessSimpleContract.cs");
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                StorageValue value1 = stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment"));
                Assert.NotNull(value1);
                Assert.Equal("1.1", value1.Version); // Block height 1, tx index 1.

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

                await node1.BroadcastTransactionAsync(callTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt callReceipt = receiptRepository.Retrieve(callTransaction.GetHash());
                Assert.True(callReceipt.Success);

                StorageValue value2 = stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment"));
                Assert.Equal("2.1", value2.Version); // Block height 1, tx index 1.

                // Check that our readwriteset contains the version for the key we read. TODO: Parse the json.
                Assert.Contains("\"1.1\"", callReceipt.ReadWriteSet);
            }
        }

        [Fact]
        public async Task MultipleTokenlessNodesCreateAndCallAContractAsync()
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

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate(), node3.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node3.DataFolder, this.network);

                node1.Start();
                node2.Start();
                node3.Start();

                TestHelper.Connect(node1, node2);
                TestHelper.Connect(node2, node3);
                TestHelper.Connect(node1, node3);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/TokenlessSimpleContract.cs");
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2, node3);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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
                //CaClient client3 = TokenlessTestHelper.GetClientAndCreateAccount(server);
                CoreNode node3 = nodeBuilder.CreateTokenlessNode(this.network, 2, server);

                certificates.Add(node3.ClientCertificate.ToCertificate());

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node3.DataFolder, this.network);

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
                var client = TokenlessTestHelper.GetAdminClient(server);
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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

                // Try and send the same transaction again. This should fail.
                var failResponse = await node1Controller.SendTransactionAsync(new SendTransactionModel()
                {
                    TransactionHex = opReturnResponse.Hex
                });

                Assert.IsType<ErrorResult>(failResponse);

                Assert.Empty(node1.FullNode.MempoolManager().GetMempoolAsync().Result);
                Assert.Empty(node2.FullNode.MempoolManager().GetMempoolAsync().Result);

                // Build and send the same transaction newly and sign it.
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


        [Fact]
        public async Task CanSaveSingleCharString()
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

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var node1Controller = node1.FullNode.NodeController<TokenlessController>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StoreSingleChar.cs");

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

                // Get the result from the controller for the multiple
                var storageResult = (JsonResult) node1Controller.GetStorage(new GetStorageRequest
                {
                    ContractAddress = createResponse.NewContractAddress,
                    DataType = MethodParameterDataType.String,
                    StorageKey = "Multiple"
                });

                string storageValue = (string) storageResult.Value;
                Assert.Equal("123", storageValue);

                // And for the single
                storageResult = (JsonResult)node1Controller.GetStorage(new GetStorageRequest
                {
                    ContractAddress = createResponse.NewContractAddress,
                    DataType = MethodParameterDataType.String,
                    StorageKey = "Single"
                });
                storageValue = (string)storageResult.Value;

                Assert.Equal("1", storageValue);
            }
        }

        [Fact(Skip = "This is useful locally to test the Swagger UI for Pure methods.")]
        public async Task SwaggerRendersPureMethodsAsync()
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

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node1.DataFolder, this.network);
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, node2.DataFolder, this.network);

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
