using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeTests
    {
        private readonly TokenlessNetwork network;
        private readonly string BaseAddress = "http://localhost:5050";

        public TokenlessNodeTests()
        {
            this.network = new TokenlessNetwork();
        }

        // TODO: Lots of repetition in this file.

        [Fact]
        public async Task StartCACorrectlyAndTestApiAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                DateTime testDate = DateTime.Now.ToUniversalTime().Date;

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create a node so we have 1 available public key.
                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

                // Get the date again in case it has changed. The idea is that the certificate date will be one of the two dates. 
                // Either the initial one or the second one if a date change occurred while the certificates were being generated.
                DateTime testDate2 = DateTime.Now.ToUniversalTime().Date;

                // Check that Authority Certificate is valid from the expected date.
                Assert.True((testDate == ac.NotBefore) || (testDate2 == ac.NotBefore));

                // Check that Authority Certificate is valid for the expected number of years.
                Assert.Equal(ac.NotBefore.AddYears(CaCertificatesManager.CaCertificateValidityPeriodYears), ac.NotAfter);

                // Get Client Certificate.
                List<CertificateInfoModel> nodeCerts = client.GetAllCertificates();
                var certParser = new X509CertificateParser();
                X509Certificate nodeCert = certParser.ReadCertificate(nodeCerts.First().CertificateContentDer);

                // Check that Client Certificate is valid from the expected date.
                Assert.True((testDate == nodeCert.NotBefore) || (testDate2 == nodeCert.NotBefore));

                // Check that Client Certificate is valid for the expected number of years.
                Assert.Equal(nodeCert.NotBefore.AddYears(CaCertificatesManager.CertificateValidityPeriodYears), nodeCert.NotAfter);

                // Get public keys from the API.
                List<PubKey> pubkeys = await client.GetCertificatePublicKeysAsync();
                Assert.Single(pubkeys);
            }
        }

        [Fact]
        public async Task TokenlessNodesMineAnEmptyBlockAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

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
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = this.CreateBasicOpReturnTransaction(node1, txPrivKey1);
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
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();

                // Stop the nodes.
                node1.FullNode.Dispose();
                node2.FullNode.Dispose();

                // Now start the nodes without passwords.
                client = GetClient();
                (node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client, false);
                (node2, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client, false);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = this.CreateBasicOpReturnTransaction(node1, txPrivKey1);
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
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = this.CreateContractCreateTransaction(node1, txPrivKey1);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = CreateContractCallTransaction(node1, createReceipt.NewContractAddress, txPrivKey1);
                await node1.BroadcastTransactionAsync(callTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt callReceipt = receiptRepository.Retrieve(callTransaction.GetHash());
                Assert.True(callReceipt.Success);

                Assert.NotNull(stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")));
            }
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallWithControllerAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

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
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Start the network with only 2 certificates generated.
                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

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
                (CoreNode node3, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 2, ac, client);
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
        public async Task NodeStoresSendersCertificateFromApiAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = this.CreateBasicOpReturnTransaction(node1, txPrivKey1);
                await node1.BroadcastTransactionAsync(transaction);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // Other node receives and mines transaction, validating it came from a permitted sender.
                await node2.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);
                var block = node1.FullNode.ChainIndexer.GetHeader(1).Block;
                Assert.Equal(2, block.Transactions.Count);

                // On the original node, the certificate shouldn't be stored in the cache as it is from "itself"
                Assert.Null(node1.FullNode.NodeService<ICertificateCache>().GetCertificate(txPrivKey1.PubKey
                    .GetAddress(this.network).ToString().ToUint160(this.network)));

                // Check that the certificate is now stored on the node.
                Assert.NotNull(node2.FullNode.NodeService<ICertificateCache>().GetCertificate(txPrivKey1.PubKey
                    .GetAddress(this.network).ToString().ToUint160(this.network)));

                // Send another transaction from the same address.
                transaction = this.CreateBasicOpReturnTransaction(node1, txPrivKey1);
                await node1.BroadcastTransactionAsync(transaction);

                // Other node receives and mines transaction, validating it came from a permitted sender, having got the certificate locally this time.
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node2.MineBlocksAsync(1);
            }
        }

        [Fact]
        public async Task TokenlessNodesFunctionIfCATurnsOff()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Turn the CA off
                server.Dispose();

                // Build and send a transaction from one node.
                Transaction transaction = this.CreateBasicOpReturnTransaction(node1, txPrivKey1);
                await node1.BroadcastTransactionAsync(transaction);

                // Node1 should still let it in the mempool as it's from himself.
                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // Node2 won't be able to get the certificate so will decline the transaction but still work.
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // First node mines a block so the transaction is in a block, which the other guy then also trusts.
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                var block = node2.FullNode.ChainIndexer.GetHeader(1).Block;
                Assert.Equal(2, block.Transactions.Count);
            }
        }

        [Fact]
        public async Task RestartCAAndEverythingStillWorksAsync()
        {
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                string dataFolderName = GetDataFolderName();
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = GetClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                }

                // Server has been killed. Restart it.

                using (IWebHost server = CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Check that we can still create nodes and make API calls.
                    (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                    List<CertificateInfoModel> nodeCerts = client.GetAllCertificates();
                    Assert.Equal(2, nodeCerts.Count);

                    List<PubKey> pubkeys = await client.GetCertificatePublicKeysAsync();
                    Assert.Equal(2, pubkeys.Count);
                }
            }
        }

        [Fact]
        public async Task RestartNodeWithoutCARemembersWhichCertificatesRevokedAsync()
        {
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                string dataFolderName = GetDataFolderName();
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = GetClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

                    // Revoke a certificate.
                    this.RevokeCertificateFromInitializedCAServer(server);
                    
                    // Get the thumbprint of the revoked certificate.
                    string revokedThumbprint = client.GetRevokedCertificates().First();

                    // Start the node.
                    node1.Start();

                    // Confirm that the certificate is revoked.
                    RevocationChecker revocationChecker = node1.FullNode.NodeService<RevocationChecker>();
                    TestBase.WaitLoop(() => revocationChecker.IsCertificateRevokedAsync(revokedThumbprint).GetAwaiter().GetResult());

                    // Stop the node.
                    node1.FullNode.Dispose();

                    // Stop the CA.
                    server.Dispose();

                    // Restart the node.
                    (node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client, false);
                    node1.Start();

                    // Is the certificate stil revoked even though we are running without a CA?
                    Assert.True(revocationChecker.IsCertificateRevokedAsync(revokedThumbprint).GetAwaiter().GetResult());
                }
            }
        }

        [Fact]
        public async Task RestartTokenlessNodeAfterBlocksMinedAndContinuesAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (var nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create a Tokenless node with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

                node1.Start();

                // Mine 20 blocks
                await node1.MineBlocksAsync(20);
                TestHelper.IsNodeSyncedAtHeight(node1, 20);

                // Restart the node and ensure that it is still at height 20.
                node1.Restart();
                TestHelper.IsNodeSyncedAtHeight(node1, 20);
            }
        }

        [Fact]
        public void CantInitializeCATwice()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Try and initialize it again with a new password.
                Assert.False(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, "SomeRandomPassword", this.network));

                // Check that the certificate is identical
                X509Certificate ac2 = GetCertificateFromInitializedCAServer(server);

                Assert.Equal(ac, ac2);
            }
        }

        [Fact]
        public async Task AddedNodeCanMineWithoutBreaking()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);
                (CoreNode node3, Key privKey3, Key txPrivKey3) = nodeBuilder.CreateFullTokenlessNode(this.network, 2, ac, client);

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
                (CoreNode node4, Key privKey4, Key txPrivKey4) = nodeBuilder.CreateFullTokenlessNode(this.network, 3, ac, client);
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
        public async Task TokenlessNodesCanSendSameOpReturnDataTwiceAsync()
        {
            using (IWebHost server = CreateWebHostBuilder(GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

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
                
                var sendTransactionResult = (JsonResult) result;
                var sendTransactionResponse = (Bitcoin.Features.MemoryPool.Broadcasting.SendTransactionModel)sendTransactionResult.Value;

                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Confirm that the tx was mined.
                Assert.True(node1.GetTip().Block.Transactions.Any(t => t.GetHash() == transactionId2));
                Assert.NotEqual(transactionId1, transactionId2);
                Assert.NotEqual(tx1.Time, tx2.Time);
            }
        }

        private CaClient GetClient()
        {
            var httpClient = new HttpClient();
            return new CaClient(new Uri(this.BaseAddress), httpClient, Settings.AdminAccountId, CaTestHelper.AdminPassword);
        }

        /// <summary>
        /// Returns the CA certificate that is stored on an initialized CA server.
        /// </summary>
        private X509Certificate GetCertificateFromInitializedCAServer(IWebHost server)
        {
            Settings settings = (Settings)server.Services.GetService(typeof(Settings));
            var acLocation = Path.Combine(settings.DataDirectory, CaCertificatesManager.CaCertFilename);
            var certParser = new X509CertificateParser();
            return certParser.ReadCertificate(File.ReadAllBytes(acLocation));
        }

        private void RevokeCertificateFromInitializedCAServer(IWebHost server)
        {
            var caCertificatesManager = (CaCertificatesManager)server.Services.GetService(typeof(CaCertificatesManager));

            // Get a good cetificate.
            var certs = caCertificatesManager.GetAllCertificates(new CredentialsAccessModel(1, CaTestHelper.AdminPassword, AccountAccessFlags.AccessAnyCertificate));

            var model = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(new CredentialsModelWithThumbprintModel()
            {
                AccountId = 1,
                Password = CaTestHelper.AdminPassword,
                Thumbprint = certs[0].Thumbprint
            }, AccountAccessFlags.RevokeCertificates);

            caCertificatesManager.RevokeCertificate(model);
        }

        private string GetDataFolderName([CallerMemberName] string callingMethod = null)
        {
            // Create a datafolder path for the CA settings to use
            string hash = Guid.NewGuid().ToString("N").Substring(0, 7);
            string numberedFolderName = string.Join(
                ".",
                new[] { hash }.Where(s => s != null));
            return Path.Combine(Path.GetTempPath(), callingMethod, numberedFolderName);
        }

        private IWebHostBuilder CreateWebHostBuilder(string dataFolderName)
        {
            var settings = new Settings();
            settings.Initialize(new string[] { $"-datadir={dataFolderName}", $"-serverurls={this.BaseAddress}" });

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<TestOnlyStartup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });

            return builder;
        }

        private Transaction CreateContractCreateTransaction(CoreNode node, Key key)
        {
            Transaction transaction = this.network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateContractCallTransaction(CoreNode node, uint160 address, Key key)
        {
            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, address, "CallMe");
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateBasicOpReturnTransaction(CoreNode node, Key key)
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }
    }
}
