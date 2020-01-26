using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Newtonsoft.Json;
using Org.BouncyCastle.Pkcs;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public class CertificateAuthorityIntegrationTests
    {
        public const int TestAccountId = 1;
        public const string TestPassword = "4815162342";
        public const string CaMnemonic = "young shoe immense usual faculty edge habit misery swarm tape viable toddler";
        public const string CaMnemonicPassword = "node";

        private readonly Network network;

        public CertificateAuthorityIntegrationTests()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public void CertificateAuthorityTestServerStartsUp()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            List<CertificateInfoModel> response = client.GetAllCertificates();

            Assert.NotNull(response);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityTestServerGetsInitialized()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword, this.network));

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanAddANewAccount()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword, this.network));

            var privateKey = new Key();
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            var accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));

            var createAccountModel = new CreateAccount()
            {
                AccountId = TestAccountId, 
                Password = TestPassword,
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

            int id = TestsHelper.GetValue<int>(accountsController.CreateAccount(createAccountModel));

            var lowPrivilegeClient = new CaClient(server.BaseAddress, server.CreateClient(), id, "test");

            // The requirement for an account to be approved prior to use must be enforced.
            // TODO: The type of exception thrown here should be more meaningful, and represent the actual error (unapproved account)
            Assert.Throws<JsonSerializationException>(() => lowPrivilegeClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes())));

            var credentialsModel = new CredentialsModelWithTargetId() {AccountId = TestAccountId, Password = TestPassword, TargetAccountId = id};

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
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword, this.network));

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
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword, this.network));

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

            CertificateInfoModel queryByAddress = client.GetCertificateForAddress(address.ToString());
            Assert.NotNull(queryByAddress.CertificateContentDer);

            server.Dispose();
        }
    }
}
