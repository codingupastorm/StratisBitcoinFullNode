using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
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

            PubKey pubKey = new Key().PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            var accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));

            var permissions = new List<Permission>() { new Permission() { Name = CaCertificatesManager.MiningPermission }, new Permission() { Name = CaCertificatesManager.SendPermission } };
            var createAccountModel = new CreateAccountModel()
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
                RequestedPermissions = permissions
            };

            int accountId = CaTestHelper.GetValue<int>(accountsController.CreateAccount(createAccountModel));

            AccountInfo account = CaTestHelper.GetValue<AccountInfo>(accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(accountId, Settings.AdminAccountId, CaTestHelper.AdminPassword)));

            Assert.Equal(AccountAccessFlags.IssueCertificates, account.AccessInfo);
            Assert.False(account.Approved);
            Assert.Equal(-1, account.ApproverId);
            Assert.Equal("dummyCountry", account.Country);
            Assert.Equal("dummyEmail@example.com", account.EmailAddress);
            Assert.Equal("dummyLocality", account.Locality);
            Assert.Equal("dummyName", account.Name);
            Assert.Equal("dummyOrganization", account.Organization);
            Assert.Equal("dummyOrganizationUnit", account.OrganizationUnit);
            Assert.Equal(2, account.Permissions.Count);
            Assert.Contains(CaCertificatesManager.MiningPermission, account.Permissions.Select(p => p.Name));
            Assert.Contains(CaCertificatesManager.SendPermission, account.Permissions.Select(p => p.Name));
            Assert.Equal("dummyState", account.StateOrProvince);

            var lowPrivilegeClient = new CaClient(server.BaseAddress, server.CreateClient(), accountId, "test");

            // The requirement for an account to be approved prior to use must be enforced.
            // TODO: The type of exception thrown here should be more meaningful, and represent the actual error (unapproved account)
            Assert.Throws<CaClientException>(() => lowPrivilegeClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes())));

            var credentialsModel = new CredentialsModelWithTargetId() { AccountId = Settings.AdminAccountId, Password = CaTestHelper.AdminPassword, TargetAccountId = accountId };

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
    }
}
