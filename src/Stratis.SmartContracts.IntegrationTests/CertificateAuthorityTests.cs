using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class CATests
    {
        private TokenlessNetwork network;

        public CATests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public void StartCACorrectlyAndTestApi()
        {
            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                DateTime testDate = DateTime.Now.ToUniversalTime().Date;

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create a node so we have 1 available public key.
                CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

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
                List<PubKey> pubkeys = client.GetCertificatePublicKeys();
                Assert.Single(pubkeys);
            }
        }

        [Fact]
        public async Task NodeStoresSendersCertificateFromApiAsync()
        {
            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
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

                CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client2);

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

                // On the original node, the certificate shouldn't be stored in the cache as it is from "itself"
                Assert.Null(node1.FullNode.NodeService<ICertificateCache>().GetCertificate(node1.TransactionSigningPrivateKey.PubKey
                    .GetAddress(this.network).ToString().ToUint160(this.network)));

                // Check that the certificate is now stored on the node.
                Assert.NotNull(node2.FullNode.NodeService<ICertificateCache>().GetCertificate(node1.TransactionSigningPrivateKey.PubKey
                    .GetAddress(this.network).ToString().ToUint160(this.network)));

                // Send another transaction from the same address.
                transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node1);
                await node1.BroadcastTransactionAsync(transaction);

                // Other node receives and mines transaction, validating it came from a permitted sender, having got the certificate locally this time.
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node2.MineBlocksAsync(1);
            }
        }

        [Fact]
        public async Task TokenlessNodesFunctionIfCATurnsOffAsync()
        {
            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
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

                CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client2);

                node1.Start();
                node2.Start();
                TestHelper.Connect(node1, node2);

                // Turn the CA off
                server.Dispose();

                // Build and send a transaction from one node.
                Transaction transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(node1);
                await node1.BroadcastTransactionAsync(transaction);

                // Node1 should still let it in the mempool as it's from himself.
                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                // Node2 won't be able to get the certificate so will decline the transaction.
                Thread.Sleep(2000);
                Assert.Empty(node2.FullNode.MempoolManager().GetMempoolAsync().Result);

                // First node mines a block so the transaction is in a block, which the other guy then also trusts.
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                var block = node2.FullNode.ChainIndexer.GetHeader(1).Block;
                Assert.Equal(2, block.Transactions.Count);
            }
        }

        [Fact]
        public void RestartCAAndEverythingStillWorks()
        {
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                string dataFolderName = TokenlessTestHelper.GetDataFolderName();
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = TokenlessTestHelper.GetAdminClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    CaClient client1 = TokenlessTestHelper.GetClient(server);

                    CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client1);
                }

                // Server has been killed. Restart it.

                using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Check that we can still create nodes and make API calls.
                    CaClient client1 = TokenlessTestHelper.GetClient(server);

                    CoreNode node2 = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client1);

                    List<CertificateInfoModel> nodeCerts = client.GetAllCertificates();
                    Assert.Equal(2, nodeCerts.Count);

                    List<PubKey> pubkeys = client.GetCertificatePublicKeys();
                    Assert.Equal(2, pubkeys.Count);
                }
            }
        }

        [Fact]
        public void RestartNodeWithoutCARemembersWhichCertificatesRevoked()
        {
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                string dataFolderName = TokenlessTestHelper.GetDataFolderName();
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(dataFolderName).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = TokenlessTestHelper.GetAdminClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client);

                    // Revoke a certificate.
                    TokenlessTestHelper.RevokeCertificateFromInitializedCAServer(server);

                    // Get the thumbprint of the revoked certificate.
                    string revokedThumbprint = client.GetRevokedCertificates().First();

                    // Start the node.
                    node1.Start();

                    // Confirm that the certificate is revoked.
                    IRevocationChecker revocationChecker = node1.FullNode.NodeService<IRevocationChecker>();
                    TestBase.WaitLoop(() => revocationChecker.IsCertificateRevoked(revokedThumbprint));

                    // Stop the node.
                    node1.FullNode.Dispose();

                    // Stop the CA.
                    server.Dispose();

                    // Restart the node.
                    node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client, false);
                    node1.Start();

                    // Is the certificate stil revoked even though we are running without a CA?
                    Assert.True(revocationChecker.IsCertificateRevoked(revokedThumbprint));
                }
            }
        }

        [Fact]
        public void CantInitializeCATwice()
        {
            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Try and initialize it again with a new password.
                Assert.False(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, "SomeRandomPassword", this.network));

                // Check that the certificate is identical
                X509Certificate ac2 = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                Assert.Equal(ac, ac2);
            }
        }
    }
}
