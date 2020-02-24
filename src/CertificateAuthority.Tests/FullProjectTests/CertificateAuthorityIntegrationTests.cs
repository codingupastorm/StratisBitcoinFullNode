using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Newtonsoft.Json;
using Org.BouncyCastle.Pkcs;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public sealed class CertificateAuthorityIntegrationTests
    {
        private readonly Network network;

        public CertificateAuthorityIntegrationTests()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public void CertificateAuthorityTestServerStartsUp()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            CaTestHelper.InitializeCa(server);

            List<CertificateInfoModel> response = client.GetAllCertificates();

            Assert.NotNull(response);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityTestServerGetsInitialized()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanAddANewAccount()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            var privateKey = new Key();
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            var accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));

            var createAccountModel = new CreateAccount()
            {
                CommonName = "dummyName",
                Country = "dummyCountry",
                EmailAddress = "dummyEmail@example.com",
                Locality = "dummyLocality",
                NewAccountPasswordHash = DataHelper.ComputeSha256Hash("test"),
                Organization = "dummyOrganization",
                OrganizationUnit = "dummyOrganizationUnit",
                StateOrProvince = "dummyState",
                RequestedAccountAccess = (int)AccountAccessFlags.IssueCertificates,
                RequestedPermissions = new List<Permission>() { new Permission() { Name = AccountsController.SendPermission } }
            };

            int id = CaTestHelper.GetValue<int>(accountsController.CreateAccount(createAccountModel));

            var lowPrivilegeClient = new CaClient(server.BaseAddress, server.CreateClient(), id, "test");

            // The requirement for an account to be approved prior to use must be enforced.
            // TODO: The type of exception thrown here should be more meaningful, and represent the actual error (unapproved account)
            Assert.Throws<CaClientException>(() => lowPrivilegeClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes())));

            var credentialsModel = new CredentialsModelWithTargetId() {AccountId = Settings.AdminAccountId, Password = CaTestHelper.AdminPassword, TargetAccountId = id};

            accountsController.ApproveAccount(credentialsModel);

            CertificateSigningRequestModel response = lowPrivilegeClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes()));

            Assert.NotNull(response);
            Assert.NotEmpty(response.CertificateSigningRequestContent);

            byte[] csrTemp = Convert.FromBase64String(response.CertificateSigningRequestContent);
            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);

            Assert.NotNull(unsignedCsr);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanGenerateCertificateSigningRequest()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            var privateKey = new Key();
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes()));

            Assert.NotNull(response);
            Assert.NotEmpty(response.CertificateSigningRequestContent);

            byte[] csrTemp = Convert.FromBase64String(response.CertificateSigningRequestContent);
            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);

            Assert.NotNull(unsignedCsr);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanIssueCertificate()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            var privateKey = new Key();

            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privateKey);

            CertificateInfoModel certInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            var certificate = new X509Certificate(certInfo.CertificateContentDer);

            Assert.NotNull(certificate);

            //Check that it's in the list of all certificates
            List<CertificateInfoModel> allCerts = client.GetAllCertificates();

            Assert.Single(allCerts);
            Assert.Equal(address.ToString(), allCerts.First().Address);

            CertificateInfoModel queryByAddress = client.GetCertificateForTransactionSigningPubKeyHash(Convert.ToBase64String(pubKey.Hash.ToBytes()));
            Assert.NotNull(queryByAddress.CertificateContentDer);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanIssueCertificateToMultipleOrganisations()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var adminClient = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(adminClient.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));
            
            var accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));

            var privateKey1 = new Key();
            PubKey pubKey1 = privateKey1.PubKey;
            BitcoinPubKeyAddress address1 = pubKey1.GetAddress(this.network);

            var createAccountModel = new CreateAccount()
            {
                Password = "test",
                CommonName = "Org1",
                Country = "UK",
                EmailAddress = "org1@example.com",
                Locality = "London",
                NewAccountPasswordHash = DataHelper.ComputeSha256Hash("test"),
                Organization = "Organisation 1",
                OrganizationUnit = "IT",
                StateOrProvince = "England",
                RequestedAccountAccess = (int)AccountAccessFlags.IssueCertificates,
                RequestedPermissions = new List<Permission>() { new Permission() { Name = AccountsController.SendPermission } }
            };

            // Create account for org 1
            int id1 = CaTestHelper.GetValue<int>(accountsController.CreateAccount(createAccountModel));

            var credentialsModel = new CredentialsModelWithTargetId() { AccountId = Settings.AdminAccountId, Password = CaTestHelper.AdminPassword, TargetAccountId = id1 };

            // Approve account 1 with the CA
            accountsController.ApproveAccount(credentialsModel);

            var privateKey2 = new Key();
            PubKey pubKey2 = privateKey2.PubKey;
            BitcoinPubKeyAddress address2 = pubKey2.GetAddress(this.network);

            var createAccountModel2 = new CreateAccount()
            {
                Password = "test",
                CommonName = "Org2",
                Country = "AU",
                EmailAddress = "org2@example.com",
                Locality = "Sydney",
                NewAccountPasswordHash = DataHelper.ComputeSha256Hash("test"),
                Organization = "Organisation 2",
                OrganizationUnit = "IT",
                StateOrProvince = "NSW",
                RequestedAccountAccess = (int)AccountAccessFlags.IssueCertificates,
                RequestedPermissions = new List<Permission>() { new Permission() { Name = AccountsController.SendPermission } }
            };

            // Create account for org 2
            int id2 = CaTestHelper.GetValue<int>(accountsController.CreateAccount(createAccountModel2));

            credentialsModel = new CredentialsModelWithTargetId() { AccountId = Settings.AdminAccountId, Password = CaTestHelper.AdminPassword, TargetAccountId = id2 };

            // Approve account 2 with the CA
            accountsController.ApproveAccount(credentialsModel);

            var client1 = new CaClient(server.BaseAddress, server.CreateClient(), id1, "test");

            CertificateInfoModel cert1 = IssueCertificate(client1, pubKey1, address1, privateKey1);
            var cert1X509 = TestCertificate(adminClient, cert1, pubKey1, address1);
            Assert.Contains(createAccountModel.Organization, cert1X509.Subject);
            
            var client2 = new CaClient(server.BaseAddress, server.CreateClient(), id2, "test");

            CertificateInfoModel cert2 = IssueCertificate(client2, pubKey2, address2, privateKey2);
            var cert2X509 = TestCertificate(adminClient, cert2, pubKey2, address2);
            Assert.Contains(createAccountModel2.Organization, cert2X509.Subject);

            server.Dispose();
        }

        private static X509Certificate TestCertificate(CaClient client, CertificateInfoModel certInfo, PubKey pubKey, BitcoinPubKeyAddress address)
        {
            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            var certificate = new X509Certificate(certInfo.CertificateContentDer);

            Assert.NotNull(certificate);

            //Check that it's in the list of all certificates
            List<CertificateInfoModel> allCerts = client.GetAllCertificates();

            CertificateInfoModel foundCert = allCerts.FirstOrDefault(a => a.Address == address.ToString());
            Assert.NotNull(foundCert);

            CertificateInfoModel queryByAddress = client.GetCertificateForTransactionSigningPubKeyHash(Convert.ToBase64String(pubKey.Hash.ToBytes()));
            Assert.NotNull(queryByAddress.CertificateContentDer);

            return certificate;
        }

        private static CertificateInfoModel IssueCertificate(CaClient client, PubKey pubKey,
            BitcoinPubKeyAddress address, Key privateKey)
        {
            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(
                Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()),
                Convert.ToBase64String(pubKey.ToBytes()));

            string signedCsr =
                CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privateKey);

            return client.IssueCertificate(signedCsr);
        }
    }
}
