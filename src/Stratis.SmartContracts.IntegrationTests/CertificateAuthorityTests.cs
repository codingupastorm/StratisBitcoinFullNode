using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.Features.PoA.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class CertificateAuthorityTests
    {
        private readonly TokenlessNetwork network;

        public CertificateAuthorityTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public void StartCACorrectlyAndTestApi()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                server.Start();

                DateTime testDate = DateTime.Now.ToUniversalTime().Date;

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                // Create a node so we have 1 available public key.
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));

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
        public async Task TokenlessNodesFunctionIfCATurnsOffAsync()
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
                CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);
                CaClient client2 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client2);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate(), node2.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));
                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node2.DataFolder, this.network.RootFolderName, this.network.Name));

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

                // Node2 utilises its local MSD to retrieve Node1's certificate, so it should be able to validate the transaction and allow it into the mempool.
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

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
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                //string dataFolderName = TokenlessTestHelper.GetDataFolderName();
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = TokenlessTestHelper.GetAdminClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                    CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client1);
                }

                // Server has been killed. Restart it.

                using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
                {
                    server.Start();

                    // Check that we can still create nodes and make API calls.
                    CaClient client1 = TokenlessTestHelper.GetClientAndCreateAdminAccount(server);

                    CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, ac, client1);

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
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                X509Certificate ac = null;
                CaClient client = null;

                using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = TokenlessTestHelper.GetAdminClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    // Get Authority Certificate.
                    ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                    // Create 1 tokenless node.
                    CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client);

                    var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate() };

                    TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));

                    // Revoke a certificate.
                    TokenlessTestHelper.RevokeCertificateFromInitializedCAServer(server);

                    // Get the thumbprint of the revoked certificate.
                    string revokedThumbprint = client.GetRevokedCertificates().First();

                    // Flag the certificate as revoked in node1's MSD
                    Directory.CreateDirectory(Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name, LocalMembershipServicesConfiguration.Crls));
                    FileStream file = File.Create(Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name, LocalMembershipServicesConfiguration.Crls, revokedThumbprint));
                    file.Flush();
                    file.Dispose();

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
                    node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, ac, client, initialRun: false);
                    node1.Start();

                    // Is the certificate stil revoked even though we are running without a CA?
                    Assert.True(revocationChecker.IsCertificateRevoked(revokedThumbprint));
                }
            }
        }

        [Fact]
        public void CantInitializeCATwice()
        {
            TestBase.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
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
