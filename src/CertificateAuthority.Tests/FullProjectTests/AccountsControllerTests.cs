using System.Collections.Generic;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public sealed class AccountsControllerTests
    {
        private readonly AccountsController accountsController;
        private readonly CredentialsModel adminCredentials;
        private readonly DataCacheLayer dataCacheLayer;

        public AccountsControllerTests()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();
            var server = new TestServer(builder);

            this.adminCredentials = new CredentialsModel(1, "4815162342");

            this.accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));
            this.dataCacheLayer = (DataCacheLayer)server.Host.Services.GetService(typeof(DataCacheLayer));
        }

        [Fact]
        private void TestAccountsControllerMethods()
        {
            // Just admin on start.
            Assert.Single(this.accountsController.GetAllAccounts(this.adminCredentials).Value);

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates;
            CredentialsModel credentials1 = TestsHelper.CreateAccount(credentials1Access);
            CredentialsModel credentials2 = TestsHelper.CreateAccount(AccountAccessFlags.DeleteAccounts);
            CredentialsModel accToDelete = TestsHelper.CreateAccount();

            // GetAccountInfoById
            {
                // Admin can access new user's data
                AccountInfo info = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password)).Value;
                Assert.Equal(credentials1Access, info.AccessInfo);
                Assert.Equal(this.adminCredentials.AccountId, info.CreatorId);

                // First user can access admin's data'
                AccountInfo info2 = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(this.adminCredentials.AccountId, credentials1.AccountId, credentials1.Password)).Value;
                Assert.Equal(this.adminCredentials.AccountId, info2.CreatorId);
                Assert.Equal(Settings.AdminName, info2.Name);

                // Guy without rights fails.
                ActionResult<AccountInfo> result = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, credentials2.AccountId, credentials2.Password));

                Assert.True(((StatusCodeResult)result.Result).StatusCode == 403);
            }

            // GetAllAccounts
            List<AccountModel> allAccounts = this.accountsController.GetAllAccounts(this.adminCredentials).Value;
            Assert.Equal(4, allAccounts.Count);

            // DeleteAccountByAccountId
            {
                this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(accToDelete.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.Equal(3, this.accountsController.GetAllAccounts(this.adminCredentials).Value.Count);

                ActionResult result = this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(credentials2.AccountId, credentials1.AccountId, credentials1.Password));
                Assert.True(((StatusCodeResult)result).StatusCode == 403);
            }

            // ChangeAccountAccessLevel
            int newFlag = 8 + 16 + 2 + 64;
            this.accountsController.ChangeAccountAccessLevel(new ChangeAccountAccessLevel(newFlag, credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password));

            int newAccessInfo = (int)this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password)).Value.AccessInfo;
            Assert.Equal(newFlag, newAccessInfo);

            // GetCertIdsIssuedByAccountId
            {
                int issuerId = credentials1.AccountId;

                string print1 = TestsHelper.GenerateRandomString(20);
                string print2 = TestsHelper.GenerateRandomString(20);

                // Add fake certificates using data repository.
                this.dataCacheLayer.AddNewCertificate(new CertificateInfoModel()
                { IssuerAccountId = issuerId, CertificateContentDer = TestsHelper.GenerateRandomString(50), Status = CertificateStatus.Good, Thumbprint = print1 });

                this.dataCacheLayer.AddNewCertificate(new CertificateInfoModel()
                { IssuerAccountId = issuerId, CertificateContentDer = TestsHelper.GenerateRandomString(50), Status = CertificateStatus.Good, Thumbprint = print2 });

                List<CertificateInfoModel> certs = this.accountsController.GetCertificatesIssuedByAccountId(new CredentialsModelWithTargetId(issuerId, this.adminCredentials.AccountId, this.adminCredentials.Password)).Value;

                Assert.Equal(2, certs.Count);
                Assert.Equal(50, certs[0].CertificateContentDer.Length);
            }
        }
    }
}
