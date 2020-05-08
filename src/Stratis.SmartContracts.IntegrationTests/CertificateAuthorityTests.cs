﻿using System;
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
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless.Networks;
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

                // Create a node so we have 1 available public key.
                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);

                var certificates = new List<X509Certificate>() { node1.ClientCertificate.ToCertificate() };

                TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, Path.Combine(node1.DataFolder, this.network.RootFolderName, this.network.Name));

                // Get the date again in case it has changed. The idea is that the certificate date will be one of the two dates. 
                // Either the initial one or the second one if a date change occurred while the certificates were being generated.
                DateTime testDate2 = DateTime.Now.ToUniversalTime().Date;

                // Check that Authority Certificate is valid from the expected date.
                Assert.True((testDate == node1.AuthorityCertificate.NotBefore) || (testDate2 == node1.AuthorityCertificate.NotBefore));

                // Check that Authority Certificate is valid for the expected number of years.
                Assert.Equal(node1.AuthorityCertificate.NotBefore.AddYears(CaCertificatesManager.CaCertificateValidityPeriodYears), node1.AuthorityCertificate.NotAfter);

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

                CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

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
                CaClient client = null;

                using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
                {
                    server.Start();

                    // Start + Initialize CA.
                    client = TokenlessTestHelper.GetAdminClient();
                    Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                    CoreNode node1 = nodeBuilder.CreateTokenlessNode(this.network, 0, server);
                }

                // Server has been killed. Restart it.
                using (IWebHost server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build())
                {
                    server.Start();

                    CoreNode node2 = nodeBuilder.CreateTokenlessNode(this.network, 1, server);

                    List<CertificateInfoModel> nodeCerts = client.GetAllCertificates();
                    Assert.Equal(2, nodeCerts.Count);

                    List<PubKey> pubkeys = client.GetCertificatePublicKeys();
                    Assert.Equal(2, pubkeys.Count);
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
