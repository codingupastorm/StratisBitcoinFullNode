﻿using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.FullProjectTests;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
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

        public TokenlessNodeTests()
        {
            this.network = new TokenlessNetwork();
        }

        [Fact]
        public async Task TokenlessNodesConnectAndMineOpReturnAsync()
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseStartup<TestOnlyStartup>();

            using (var server = new TestServer(builder))
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                // Start + Initialize CA.
                var client = new CaClient(server.BaseAddress, server.CreateClient(), CertificateAuthorityIntegrationTests.TestAccountId, CertificateAuthorityIntegrationTests.TestPassword);
                Assert.True(client.InitializeCertificateAuthority(CertificateAuthorityIntegrationTests.CaMnemonic, CertificateAuthorityIntegrationTests.CaMnemonicPassword));

                // Get Authority Certificate.
                Settings settings = (Settings)server.Host.Services.GetService(typeof(Settings));
                var acLocation = Path.Combine(settings.DataDirectory, CaCertificatesManager.CaCertFilename);
                X509Certificate2 ac = new X509Certificate2(File.ReadAllBytes(acLocation));


                // Create 2 new client certificates.
                var privKey1 = new Key();
                PubKey pubKey1 = privKey1.PubKey;
                BitcoinPubKeyAddress address1 = pubKey1.GetAddress(this.network);
                X509Certificate2 certificate1 = IssueCertificate(client, privKey1, pubKey1, address1);
                Assert.NotNull(certificate1);

                var privKey2 = new Key();
                PubKey pubKey2 = privKey2.PubKey;
                BitcoinPubKeyAddress address2 = pubKey2.GetAddress(this.network);
                X509Certificate2 certificate2 = IssueCertificate(client, privKey2, pubKey2, address2);
                Assert.NotNull(certificate2);

                // Create 2 Tokenless nodes, each with the Authority Certificate and 1 client certificate in their NodeData folder.  
                CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, certificate1);
                CoreNode node2 = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, certificate2);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Build and send a transaction from one node.
                Transaction transaction = this.CreateBasicOpReturnTransaction(node1);
                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                await broadcasterManager.BroadcastTransactionAsync(transaction);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);


                // Other node receives and mines transaction, validating it came from a permitted sender.
                await node2.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node1.FullNode.ChainIndexer.Height == 1);
                Assert.Equal(2, node1.FullNode.ChainIndexer.GetHeader(1).Block.Transactions.Count); // TODO: Double check we have a coinbase? Otherwise this is just 1.
            }
        }

        private X509Certificate2 IssueCertificate(CaClient client, Key privKey, PubKey pubKey, BitcoinPubKeyAddress address)
        {
            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString());

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privKey);

            CertificateInfoModel certInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            return new X509Certificate2(Convert.FromBase64String(certInfo.CertificateContentDer));
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallAContractAsync()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateFullTokenlessNode(this.network, 0);
                CoreNode node2 = builder.CreateFullTokenlessNode(this.network, 1);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = this.CreateContractCreateTransaction(node1);
                await broadcasterManager.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 1);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = CreateContractCallTransaction(node1, createReceipt.NewContractAddress);
                await broadcasterManager.BroadcastTransactionAsync(callTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 2);

                Receipt callReceipt = receiptRepository.Retrieve(callTransaction.GetHash());
                Assert.True(callReceipt.Success);

                Assert.NotNull(stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")));
            }
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallWithControllerAsync()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateFullTokenlessNode(this.network, 0);
                CoreNode node2 = builder.CreateFullTokenlessNode(this.network, 1);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var node1Controller = node1.FullNode.NodeController<TokenlessController>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();

                ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");

                var createModel = new BuildCreateContractTransactionModel()
                {
                    Mnemonic = "lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom",
                    ContractCode = compilationResult.Compilation
                };

                var createResult = (JsonResult)node1Controller.BuildCreateContractTransaction(createModel);
                var createResponse = (BuildCreateContractTransactionResponse)createResult.Value;

                node1Controller.SendTransactionAsync(createResponse.Hex);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 1);

                Receipt createReceipt = receiptRepository.Retrieve(createResponse.TransactionId);
                Assert.True(createReceipt.Success);

                var callModel = new BuildCallContractTransactionModel()
                {
                    Mnemonic = "lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom",
                    Address = createReceipt.NewContractAddress.ToBase58Address(this.network),
                    MethodName = "CallMe"
                };

                var callResult = (JsonResult)node1Controller.BuildCallContractTransaction(callModel);
                var callResponse = (BuildCallContractTransactionResponse)callResult.Value;

                node1Controller.SendTransactionAsync(callResponse.Hex);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 2);

                Receipt callReceipt = receiptRepository.Retrieve(callResponse.TransactionId);
                Assert.True(callReceipt.Success);
            }
        }

        private Transaction CreateContractCreateTransaction(CoreNode node)
        {
            Transaction transaction = this.network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateContractCallTransaction(CoreNode node, uint160 address)
        {
            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, address, "CallMe");
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateBasicOpReturnTransaction(CoreNode node)
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }
    }
}
