using System.Collections.Generic;
using System.Linq;
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
        private readonly TestServer server;

        public AccountsControllerTests()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();
            this.server = new TestServer(builder);

            this.adminCredentials = new CredentialsModel(1, "4815162342");
            this.accountsController = (AccountsController)this.server.Host.Services.GetService(typeof(AccountsController));
            this.dataCacheLayer = (DataCacheLayer)this.server.Host.Services.GetService(typeof(DataCacheLayer));
        }

        [Fact]
        private void TestAccountsControllerMethods()
        {
            // Just admin on start.
            Assert.Single(TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(this.adminCredentials)));

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates;
            CredentialsModel credentials1 = TestsHelper.CreateAccount(this.server, credentials1Access);
            CredentialsModel credentials2 = TestsHelper.CreateAccount(this.server, AccountAccessFlags.DeleteAccounts);
            CredentialsModel accToDelete = TestsHelper.CreateAccount(this.server);

            // GetAccountInfoById
            {
                // Admin can access new user's data
                AccountInfo info = TestsHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password)));
                Assert.Equal(credentials1Access, info.AccessInfo);
                Assert.Equal(this.adminCredentials.AccountId, info.CreatorId);

                // First user can access admin's data'
                AccountInfo info2 = TestsHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(this.adminCredentials.AccountId, credentials1.AccountId, credentials1.Password)));
                Assert.Equal(this.adminCredentials.AccountId, info2.CreatorId);
                Assert.Equal(Settings.AdminName, info2.Name);

                // User without rights fails.
                IActionResult result = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.True(((StatusCodeResult)result).StatusCode == 403);
            }

            // GetAllAccounts
            List<AccountModel> allAccounts = TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(this.adminCredentials));
            Assert.Equal(4, allAccounts.Count);

            // DeleteAccountByAccountId
            {
                this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(accToDelete.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.Equal(3, TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(this.adminCredentials)).Count);

                IActionResult result = this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(credentials2.AccountId, credentials1.AccountId, credentials1.Password));
                Assert.True(((StatusCodeResult)result).StatusCode == 403);
            }

            // ChangeAccountAccessLevel
            int newFlag = 8 + 16 + 2 + 64;
            this.accountsController.ChangeAccountAccessLevel(new ChangeAccountAccessLevel(newFlag, credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password));

            int newAccessInfo = (int)TestsHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password))).AccessInfo;
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

                List<CertificateInfoModel> certs = TestsHelper.GetValue<List<CertificateInfoModel>>(this.accountsController.GetCertificatesIssuedByAccountId(new CredentialsModelWithTargetId(issuerId, this.adminCredentials.AccountId, this.adminCredentials.Password)));

                Assert.Equal(2, certs.Count);
                Assert.Equal(50, certs[0].CertificateContentDer.Length);
            }
        }

        [Fact]
        public void ChangeAccountPassword_CurrentUser_Pass()
        {
            CredentialsModel credentials = TestsHelper.CreateAccount(this.server, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(credentials.AccountId, credentials.AccountId, credentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == credentials.AccountId);
            Assert.True(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_CurrentUser_WrongPassword_Fail()
        {
            CredentialsModel credentials = TestsHelper.CreateAccount(this.server, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(credentials.AccountId, credentials.AccountId, "wrongpassword", "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == credentials.AccountId);
            Assert.False(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_AdminUser_Pass()
        {
            CredentialsModel userA_Credentials = TestsHelper.CreateAccount(this.server, AccountAccessFlags.BasicAccess);

            var changePasswordModel = new ChangeAccountPasswordModel(this.adminCredentials.AccountId, userA_Credentials.AccountId, this.adminCredentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(changePasswordModel);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == userA_Credentials.AccountId);
            Assert.True(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_DifferentUser_Fail()
        {
            CredentialsModel userA_Credentials = TestsHelper.CreateAccount(this.server, AccountAccessFlags.BasicAccess);
            CredentialsModel userB_Credentials = TestsHelper.CreateAccount(this.server, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(userA_Credentials.AccountId, userB_Credentials.AccountId, userA_Credentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = TestsHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(adminCredentialsModel));
            AccountModel userB_Account = accounts.FirstOrDefault(a => a.Id == userB_Credentials.AccountId);
            Assert.False(userB_Account.VerifyPassword("newpassword"));
            Assert.True(userB_Account.VerifyPassword(userB_Credentials.Password));
        }
    }
}