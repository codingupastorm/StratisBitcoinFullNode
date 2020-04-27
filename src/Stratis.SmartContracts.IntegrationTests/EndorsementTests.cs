﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Nethereum.Hex.HexConvertors.Extensions;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.Features.PoA.Tests.Common;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class EndorsementTests
    {
        // This comes from CaTestHelper. 
        private const string OrganisationName = "dummyOrganization";

        private TokenlessNetwork network;

        public EndorsementTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public async Task EndorseCallTransaction()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
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

                EndorsementPolicy policy = new EndorsementPolicy
                {
                    Organisation = (Organisation) node1.ClientCertificate.ToCertificate().GetOrganisation(),
                    RequiredSignatures = 1
                };

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/TokenlessSimpleContract.cs", policy);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

                var tokenlessController = node1.FullNode.NodeController<TokenlessController>();
                JsonResult result = (JsonResult) await tokenlessController.SendProposalAsync(new SendProposalModel
                {
                    TransactionHex = callTransaction.ToHex(),
                    Organisation = OrganisationName
                });

                var endorsementResponse = (SendProposalResponseModel) result.Value;
                Assert.Equal("Transaction has been sent to endorsing node for execution.", endorsementResponse.Message);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().InfoAll().Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().InfoAll().Count > 0);

                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Assert.Equal(BitConverter.GetBytes(101), stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")).Value);
            }
        }

        [Fact]
        public async Task CallTransaction_MultipleSignatures()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
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
                CaClient client3 = TokenlessTestHelper.GetClient(server);

                // Proposer
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);

                // Endorser 1
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                // Endorser 2
                CoreNode node3 = nodeBuilder.CreateTokenlessNode(this.network, 2, ac, client3);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate(), node3.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node3.DataFolder, this.network.RootFolderName, this.network.Name));

                node1.Start();
                node2.Start();
                node3.Start();

                TestHelper.Connect(node1, node2);
                TestHelper.Connect(node1, node3);
                TestHelper.Connect(node2, node3);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                EndorsementPolicy policy = new EndorsementPolicy
                {
                    Organisation = (Organisation)node1.ClientCertificate.ToCertificate().GetOrganisation(),
                    RequiredSignatures = 2
                };

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/TokenlessSimpleContract.cs", policy);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

                var tokenlessController = node1.FullNode.NodeController<TokenlessController>();
                JsonResult result = (JsonResult)await tokenlessController.SendProposalAsync(new SendProposalModel
                {
                    TransactionHex = callTransaction.ToHex(),
                    Organisation = OrganisationName
                });

                var endorsementResponse = (SendProposalResponseModel)result.Value;
                Assert.Equal("Transaction has been sent to endorsing node for execution.", endorsementResponse.Message);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().InfoAll().Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().InfoAll().Count > 0);

                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Assert.Equal(BitConverter.GetBytes(101), stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")).Value);
            }
        }

        [Fact]
        public async Task PrivateDataTransaction()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
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

                EndorsementPolicy policy = new EndorsementPolicy
                {
                    Organisation = (Organisation)node1.ClientCertificate.ToCertificate().GetOrganisation(),
                    RequiredSignatures = 1
                };

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/PrivateDataContract.cs", policy);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                EndorsementPolicy savedPolicy = stateRepo.GetPolicy(createReceipt.NewContractAddress);

                Assert.Equal(policy.Organisation, savedPolicy.Organisation);
                Assert.Equal(policy.RequiredSignatures, savedPolicy.RequiredSignatures);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "StoreTransientData");

                byte[] transientDataToStore = new byte[] { 0, 1, 2, 3 };

                var tokenlessController = node1.FullNode.NodeController<TokenlessController>();
                JsonResult result = (JsonResult)await tokenlessController.SendProposalAsync(new SendProposalModel
                {
                    TransactionHex = callTransaction.ToHex(),
                    Organisation = OrganisationName,
                    TransientDataHex = transientDataToStore.ToHex()
                });

                var endorsementResponse = (SendProposalResponseModel)result.Value;
                Assert.Equal("Transaction has been sent to endorsing node for execution.", endorsementResponse.Message);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().InfoAll().Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().InfoAll().Count > 0);

                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                // Check that the transient data was stored in the non-private store.
                Assert.Equal(transientDataToStore, stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Transient")).Value);

                // And that it was stored in the transient store on both nodes!
                var lastBlock = node1.FullNode.BlockStore().GetBlock(node1.FullNode.ChainIndexer.Tip.HashBlock);
                var rwsTransaction = lastBlock.Transactions[1];
                Assert.NotNull(node1.FullNode.NodeService<ITransientStore>().Get(rwsTransaction.GetHash()));
                Assert.NotNull(node2.FullNode.NodeService<ITransientStore>().Get(rwsTransaction.GetHash()));

                Thread.Sleep(2000);

                Assert.NotNull(node2.FullNode.NodeService<IPrivateDataStore>().GetBytes(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("TransientPrivate")));
                Assert.NotNull(node2.FullNode.NodeService<IPrivateDataStore>().GetBytes(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("TransientPrivate")));
            }
        }

        [Fact]
        public async Task InvalidTransactionNotIncludedInBlock()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
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

                EndorsementPolicy policy = new EndorsementPolicy
                {
                    Organisation = (Organisation)node1.ClientCertificate.ToCertificate().GetOrganisation(),
                    RequiredSignatures = 1
                };

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey, "SmartContracts/TokenlessSimpleContract.cs", policy);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                // Now we put a CALL into the mempool
                Transaction standardTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");
                await node1.BroadcastTransactionAsync(standardTransaction);

                // And then get a transaction endorsed.
                Transaction endorsed = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey, "CallMe");

                var tokenlessController = node1.FullNode.NodeController<TokenlessController>();
                JsonResult result = (JsonResult)await tokenlessController.SendProposalAsync(new SendProposalModel
                {
                    TransactionHex = endorsed.ToHex(),
                    Organisation = OrganisationName
                });

                var endorsementResponse = (SendProposalResponseModel)result.Value;
                Assert.Equal("Transaction has been sent to endorsing node for execution.", endorsementResponse.Message);

                // Our mempool should now contain the original call, and the endorsed transaction.

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().InfoAll().Count == 2);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().InfoAll().Count == 2);

                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSyncAvoidMempool(node1, node2);

                // But only the initial CALL should be in the block
                NBitcoin.Block lastBlock = node1.FullNode.BlockStore().GetBlock(node1.FullNode.ChainIndexer.Tip.HashBlock);
                Assert.Equal(2, lastBlock.Transactions.Count);
                Assert.Equal(standardTransaction.GetHash(), lastBlock.Transactions[1].GetHash());

                // TODO: node2 still has the invalid transaction in its mempool. It needs to be taken out via Signals?
            }
        }
    }
}
