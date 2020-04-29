using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public sealed class AccountsControllerTests
    {
        private AccountsController accountsController;
        private readonly CredentialsModel adminCredentials;
        private DataCacheLayer dataCacheLayer;
        private TestServer server;

        public AccountsControllerTests()
        {
            this.adminCredentials = new CredentialsModel(Settings.AdminAccountId, CaTestHelper.AdminPassword);
        }

        private void CreateServer([CallerMemberName] string callingMethod = "")
        {
            TestBase.GetTestRootFolder(out string testRootFolder, callingMethod);
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder(testRootFolder);
            this.server = new TestServer(builder);

            this.accountsController = (AccountsController)this.server.Host.Services.GetService(typeof(AccountsController));
            this.dataCacheLayer = (DataCacheLayer)this.server.Host.Services.GetService(typeof(DataCacheLayer));

            CaTestHelper.InitializeCa(this.server);
        }

        [Fact]
        public void TestAccountsControllerMethods()
        {
            CreateServer();

            // Just admin on start.
            Assert.Single(CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(this.adminCredentials)));

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates;
            CredentialsModel credentials1 = CaTestHelper.CreateAccount(this.server.Host, credentials1Access);
            CredentialsModel credentials2 = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.DeleteAccounts);
            CredentialsModel accToDelete = CaTestHelper.CreateAccount(this.server.Host);

            // GetAccountInfoById
            {
                // Admin can access new user's data
                AccountInfo info = CaTestHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password)));
                Assert.Equal(credentials1Access, info.AccessInfo);

                // First user can access admin's data'
                AccountInfo info2 = CaTestHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(this.adminCredentials.AccountId, credentials1.AccountId, credentials1.Password)));
                Assert.Equal(this.adminCredentials.AccountId, info2.ApproverId);
                Assert.Equal(Settings.AdminName, info2.Name);

                // User without rights fails.
                IActionResult result = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.True(((ObjectResult)result).StatusCode == 403);
            }

            // ListAll
            List<AccountModel> allAccounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(this.adminCredentials));
            Assert.Equal(4, allAccounts.Count);

            // DeleteAccountByAccountId
            {
                this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(accToDelete.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.Equal(3, CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(this.adminCredentials)).Count);

                IActionResult result = this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(credentials2.AccountId, credentials1.AccountId, credentials1.Password));
                Assert.True(((ObjectResult)result).StatusCode == 403);
            }

            // ChangeAccountAccessLevel
            int newFlag = 8 + 16 + 2 + 64;
            this.accountsController.ChangeAccountAccessLevel(new ChangeAccountAccessLevel(newFlag, credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password));

            int newAccessInfo = (int)CaTestHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password))).AccessInfo;
            Assert.Equal(newFlag, newAccessInfo);

            // GetCertIdIssuedByAccountId
            {
                int issuerId = credentials1.AccountId;

                string print1 = CaTestHelper.GenerateRandomString(20);
                byte[] blockSignPubKey1 = (new Key()).PubKey.ToBytes();
                byte[] txSignPubKeyHash1 = (new Key()).PubKey.Hash.ToBytes();

                // Add fake certificate using data repository.
                this.dataCacheLayer.AddNewCertificate(new CertificateInfoModel()
                {
                    AccountId = issuerId,
                    CertificateContentDer = new byte[50],
                    Status = CertificateStatus.Good,
                    Thumbprint = print1,
                    BlockSigningPubKey = blockSignPubKey1,
                    TransactionSigningPubKeyHash = txSignPubKeyHash1
                });

                CertificateInfoModel cert = CaTestHelper.GetValue<CertificateInfoModel>(this.accountsController.GetCertificateIssuedByAccountId(new CredentialsModelWithTargetId(issuerId, this.adminCredentials.AccountId, this.adminCredentials.Password)));

                Assert.Equal(50, cert.CertificateContentDer.Length);
                Assert.Equal(blockSignPubKey1, cert.BlockSigningPubKey);
                Assert.Equal(txSignPubKeyHash1, cert.TransactionSigningPubKeyHash);
            }
        }

        [Fact]
        public void ListUnapprovedAccounts()
        {
            CreateServer();

            _ = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess, approve: false);
            _ = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            List<AccountModel> unapprovedAccounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListUnapproved(this.adminCredentials));
            Assert.Single(unapprovedAccounts);
        }

        [Fact]
        public void ChangeAccountPassword_CurrentUser_Pass()
        {
            CreateServer();

            CredentialsModel credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(credentials.AccountId, credentials.AccountId, credentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == credentials.AccountId);
            Assert.True(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_CurrentUser_WrongPassword_Fail()
        {
            CreateServer();

            CredentialsModel credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(credentials.AccountId, credentials.AccountId, "wrongpassword", "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == credentials.AccountId);
            Assert.False(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_AdminUser_Pass()
        {
            CreateServer();

            CredentialsModel userA_Credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            var changePasswordModel = new ChangeAccountPasswordModel(this.adminCredentials.AccountId, userA_Credentials.AccountId, this.adminCredentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(changePasswordModel);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(adminCredentialsModel));
            AccountModel account = accounts.FirstOrDefault(a => a.Id == userA_Credentials.AccountId);
            Assert.True(account.VerifyPassword("newpassword"));
        }

        [Fact]
        public void ChangeAccountPassword_DifferentUser_Fail()
        {
            CreateServer();

            CredentialsModel userA_Credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);
            CredentialsModel userB_Credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            var model = new ChangeAccountPasswordModel(userA_Credentials.AccountId, userB_Credentials.AccountId, userA_Credentials.Password, "newpassword");
            this.accountsController.ChangeAccountPassword(model);

            var adminCredentialsModel = new CredentialsModel(this.adminCredentials.AccountId, this.adminCredentials.Password);
            List<AccountModel> accounts = CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.ListAll(adminCredentialsModel));
            AccountModel userB_Account = accounts.FirstOrDefault(a => a.Id == userB_Credentials.AccountId);
            Assert.False(userB_Account.VerifyPassword("newpassword"));
            Assert.True(userB_Account.VerifyPassword(userB_Credentials.Password));
        }

        [Fact]
        public void RejectAccount_Pass()
        {
            CreateServer();

            CredentialsModel userA_Credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess, approve: false);

            var model = new CredentialsModelWithTargetId(userA_Credentials.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password);
            this.accountsController.RejectAccount(model);

            AccountInfo account = CaTestHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(model));
            Assert.Null(account);
        }

        [Fact]
        public void RejectAccount_AccountAlreadyApproved_Fail()
        {
            CreateServer();

            CredentialsModel userA_Credentials = CaTestHelper.CreateAccount(this.server.Host, AccountAccessFlags.BasicAccess);

            var model = new CredentialsModelWithTargetId(userA_Credentials.AccountId, this.adminCredentials.AccountId, this.adminCredentials.Password);
            this.accountsController.RejectAccount(model);

            AccountInfo account = CaTestHelper.GetValue<AccountInfo>(this.accountsController.GetAccountInfoById(model));
            Assert.NotNull(account);
        }
    }
}
