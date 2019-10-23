using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority.Code;
using CertificateAuthority.Code.Controllers;
using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using AccountAccessFlags = CertificateAuthority.Code.Models.AccountAccessFlags;
using AccountInfo = CertificateAuthority.Code.Models.AccountInfo;
using CertificateInfoModel = CertificateAuthority.Code.Models.CertificateInfoModel;
using CertificateStatus = CertificateAuthority.Code.Models.CertificateStatus;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public class ControllersTests
    {
        private AccountsController accountsController;

        private CertificatesController certificatesController;

        private CredentialsModel adminCredentials;

        private DataRepository dataRepository;

        public ControllersTests()
        {
            StartupContainer.RequestStartupCreation();

            TestOnlyStartup startup = StartupContainer.GetStartupWhenReady();

            this.adminCredentials = new CredentialsModel(1, "4815162342");

            this.accountsController = startup.CreateAccountsController();
            this.certificatesController = startup.CreateCertificatesController();
            this.dataRepository = startup.DataRepository;
        }

        [Fact]
        private void TestAccountsControllerMethods()
        {
            // Just admin on start.
            Assert.Single(this.accountsController.GetAllAccounts(this.adminCredentials).Value);

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates;
            CredentialsModel credentials1 = this.CreateAccount(credentials1Access);
            CredentialsModel credentials2 = this.CreateAccount(AccountAccessFlags.DeleteAccounts);
            CredentialsModel accToDelete = this.CreateAccount();

            // GetAccountInfoById
            {
                // Admin can access new user's data
                AccountInfo info = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, adminCredentials.AccountId, adminCredentials.Password)).Value;
                Assert.Equal(credentials1Access, info.AccessInfo);
                Assert.Equal(this.adminCredentials.AccountId, info.CreatorId);

                // First user can access admin's data'
                AccountInfo info2 = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(this.adminCredentials.AccountId, credentials1.AccountId, credentials1.Password)).Value;
                Assert.Equal(this.adminCredentials.AccountId, info2.CreatorId);
                Assert.Equal(Settings.AdminName, info2.Name);

                // Guy without rights fails.
                Assert.ThrowsAny<Exception>(() => this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, credentials2.AccountId, credentials2.Password)));
            }

            // GetAllAccounts
            List<AccountInfo> allAccounts = this.accountsController.GetAllAccounts(this.adminCredentials).Value;
            Assert.Equal(4, allAccounts.Count);

            // DeleteAccountByAccountId
            {
                this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(accToDelete.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.Equal(3, this.accountsController.GetAllAccounts(this.adminCredentials).Value.Count);

                Assert.ThrowsAny<Exception>(() => this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(credentials2.AccountId, credentials1.AccountId, credentials1.Password)));
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
                this.dataRepository.GetCertificatesCollection().Insert(new CertificateInfoModel()
                    { IssuerAccountId = issuerId, CertificateContent = TestsHelper.GenerateRandomString(50), Status = CertificateStatus.Good, Thumbprint = print1 });

                this.dataRepository.GetCertificatesCollection().Insert(new CertificateInfoModel()
                    { IssuerAccountId = issuerId, CertificateContent = TestsHelper.GenerateRandomString(50), Status = CertificateStatus.Good, Thumbprint = print2 });

                List<CertificateInfoModel> certs = this.accountsController.GetCertificatesIssuedByAccountId(new CredentialsModelWithTargetId(issuerId, this.adminCredentials.AccountId, this.adminCredentials.Password)).Value;

                Assert.Equal(2, certs.Count);
                Assert.Equal(50, certs[0].CertificateContent.Length);
            }
        }

        [Fact]
        private async Task TestCertificatesControllerMethods()
        {
            // Just admin on start.
            Assert.Single(this.accountsController.GetAllAccounts(this.adminCredentials).Value);

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            CredentialsModel credentials1 = this.CreateAccount(credentials1Access);

            // IssueCertificate_UsingRequestString
            CertificateInfoModel certificate1 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(TestDataResource.CertificateRequest1, credentials1.AccountId, credentials1.Password))).Value;

            CertificateInfoModel certificate2 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(TestDataResource.CertificateRequest2, this.adminCredentials.AccountId, this.adminCredentials.Password))).Value;

            Assert.Empty(this.certificatesController.GetRevokedCertificates().Value);

            // GetCertificateByThumbprint
            CertificateInfoModel cert1Retrieved = this.certificatesController.GetCertificateByThumbprint(
                new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, this.adminCredentials.AccountId, this.adminCredentials.Password)).Value;
            Assert.Equal(certificate1.Id, cert1Retrieved.Id);
            Assert.Equal(certificate1.IssuerAccountId, cert1Retrieved.IssuerAccountId);

            string status = this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true)).Value;
            Assert.Equal(CertificateStatus.Good.ToString(), status);

            this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password));

            // Can't revoke 2nd time same cert.
            ActionResult<bool> result = this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password));
            Assert.False(result.Value);

            Assert.Equal(CertificateStatus.Revoked.ToString(), this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true)).Value);
            Assert.Equal(CertificateStatus.Unknown.ToString(), this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(TestsHelper.GenerateRandomString(20), true)).Value);

            List<CertificateInfoModel> allCerts = this.certificatesController.GetAllCertificates(credentials1).Value;
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Good) == 1);
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Revoked) == 1);

            Assert.Equal(CertificateStatus.Revoked.ToString(), this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true)).Value);

            List<string> revoked = this.certificatesController.GetRevokedCertificates().Value.ToList();
            Assert.Single(revoked);
            Assert.Equal(certificate1.Thumbprint, revoked[0]);
        }

        [Fact]
        private async Task TestAccessLevels()
        {
            // Accounts.
            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.GetAllAccounts(new CredentialsModel(accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.CreateAccount(new CreateAccount("", "", 1, accountId, password)),
                AccountAccessFlags.CreateAccounts);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.GetCertificatesIssuedByAccountId(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.DeleteAccounts);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.accountsController.ChangeAccountAccessLevel(new ChangeAccountAccessLevel(1, 1, accountId, password)),
                AccountAccessFlags.ChangeAccountAccessLevel);

            // Certificates.
            this.CheckThrowsIfNoAccess((int accountId, string password) => this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.RevokeCertificates);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.certificatesController.GetCertificateByThumbprint(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.certificatesController.GetAllCertificates(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.CheckThrowsIfNoAccess( (int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestFileAsync(new IssueCertificateFromRequestModel(null, accountId, password)).GetAwaiter().GetResult(),
                AccountAccessFlags.IssueCertificates);

            this.CheckThrowsIfNoAccess((int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestStringAsync(new IssueCertificateFromFileContentsModel("123", accountId, password)).GetAwaiter().GetResult(),
                AccountAccessFlags.IssueCertificates);
        }

        private CredentialsModel CreateAccount(AccountAccessFlags access = AccountAccessFlags.BasicAccess, CredentialsModel creatorCredentialsModel = null)
        {
            string password = TestsHelper.GenerateRandomString();
            string passHash = DataHelper.ComputeSha256Hash(password);

            CredentialsModel credentialsModel = creatorCredentialsModel ?? this.adminCredentials;
            int id = this.accountsController.CreateAccount(new CreateAccount(TestsHelper.GenerateRandomString(), passHash, (int)access, credentialsModel.AccountId, credentialsModel.Password)).Value;

            return new CredentialsModel(id, password);
        }

        private void CheckThrowsIfNoAccess(Action<int, string> action, AccountAccessFlags requiredAccess)
        {
            CredentialsModel noAccessCredentials = this.CreateAccount();
            bool throwsIfNoAccess = false;

            try
            {
                action.Invoke(noAccessCredentials.AccountId, noAccessCredentials.Password);
            }
            catch (InvalidCredentialsException e)
            {
                Assert.Equal(CredentialsExceptionErrorCodes.InvalidAccess, e.ErrorCode);
                throwsIfNoAccess = true;
            }
            catch (Exception e)
            {
            }

            if (!throwsIfNoAccess)
                Assert.False(true, "Action was expected to throw.");

            CredentialsModel accessCredentials = this.CreateAccount(requiredAccess);

            try
            {
                action.Invoke(accessCredentials.AccountId, accessCredentials.Password);
            }
            catch (InvalidCredentialsException e)
            {
                Assert.False(true, "Action was expected to not throw or throw different exception.");
            }
            catch (Exception)
            {
            }
        }
    }
}
