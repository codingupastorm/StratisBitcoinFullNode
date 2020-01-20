using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.FullProjectTests;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
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
        public async Task GetPublicKeysFromApi()
        {
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                // Create a node so we have 1 available public key.
                (CoreNode node1, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

                // Get public keys from the API.
                List<PubKey> pubkeys = await client.GetCertificatePublicKeysAsync();
                Assert.Single(pubkeys);
            }
        }

        [Fact]
        public async Task TokenlessNodesMineAnEmptyBlockAsync()
        {
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

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
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

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
                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                await broadcasterManager.BroadcastTransactionAsync(transaction);

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
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = GetCertificateFromInitializedCAServer(server);

                (CoreNode node1, Key privKey1, Key txPrivKey1) = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);
                (CoreNode node2, Key privKey2, Key txPrivKey2) = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = this.CreateContractCreateTransaction(node1, txPrivKey1);
                await broadcasterManager.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = CreateContractCallTransaction(node1, createReceipt.NewContractAddress, txPrivKey1);
                await broadcasterManager.BroadcastTransactionAsync(callTransaction);
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
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

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
            }
        }

        [Fact]
        public async Task TokenlessNodesKickAMinerBasedOnCA()
        {
            using (IWebHost server = CreateWebHostBuilder().Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = GetClient();
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic,
                    CertificateAuthorityIntegrationTests.CaMnemonicPassword, this.network));

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

                // Last of all, create a 3rd node and see him get voted in.
                (CoreNode node3, _, _) = nodeBuilder.CreateFullTokenlessNode(this.network, 2, ac, client);
                node3.Start();
                TestHelper.Connect(node3, node2);
                TestHelper.Connect(node3, node1);

                TestBase.WaitLoop(() => node1VotingManager.GetScheduledVotes().Count > 0);
                TestBase.WaitLoop(() => node2VotingManager.GetScheduledVotes().Count > 0);

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

        private HttpClient GetHttpClient()
        {
            // TODO: This is a massive stupid hack to test with self signed certs.
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = ((sender, cert, chain, errors) => true)
            };

            return new HttpClient(handler);
        }

        private CaClient GetClient()
        {
            // TODO: This is a massive stupid hack to test with self signed certs.
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = ((sender, cert, chain, errors) => true);
            var httpClient = GetHttpClient();
            return new CaClient(new Uri(this.BaseAddress), httpClient, CertificateAuthorityIntegrationTests.TestAccountId, CertificateAuthorityIntegrationTests.TestPassword);
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


        private IWebHostBuilder CreateWebHostBuilder([CallerMemberName] string callingMethod = null)
        {
            // Create a datafolder path for the CA settings to use
            string hash = Guid.NewGuid().ToString("N").Substring(0, 7);
            string numberedFolderName = string.Join(
                ".",
                new[] { hash }.Where(s => s != null));
            string dataFolderName = Path.Combine(Path.GetTempPath(), callingMethod, numberedFolderName);

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
