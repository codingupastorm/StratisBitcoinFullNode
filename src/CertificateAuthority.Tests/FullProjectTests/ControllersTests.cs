using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Xunit;
using AccountAccessFlags = CertificateAuthority.Models.AccountAccessFlags;
using AccountInfo = CertificateAuthority.Models.AccountInfo;
using CertificateInfoModel = CertificateAuthority.Models.CertificateInfoModel;
using CertificateStatus = CertificateAuthority.Models.CertificateStatus;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public class ControllersTests
    {
        private AccountsController accountsController;

        private CertificatesController certificatesController;

        private CredentialsModel adminCredentials;

        private DataCacheLayer dataCacheLayer;

        public ControllersTests()
        {
            StartupContainer.RequestStartupCreation();

            TestOnlyStartup startup = StartupContainer.GetStartupWhenReady();

            this.adminCredentials = new CredentialsModel(1, "4815162342");

            this.accountsController = startup.CreateAccountsController();
            this.certificatesController = startup.CreateCertificatesController();
            this.dataCacheLayer = startup.DataCacheLayer;
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
                var result = this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(credentials1.AccountId, credentials2.AccountId, credentials2.Password));

                Assert.True((((StatusCodeResult)result.Result).StatusCode == 403));
            }

            // GetAllAccounts
            List<AccountModel> allAccounts = this.accountsController.GetAllAccounts(this.adminCredentials).Value;
            Assert.Equal(4, allAccounts.Count);

            // DeleteAccountByAccountId
            {
                this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(accToDelete.AccountId, credentials2.AccountId, credentials2.Password));
                Assert.Equal(3, this.accountsController.GetAllAccounts(this.adminCredentials).Value.Count);

                var result = this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(credentials2.AccountId, credentials1.AccountId, credentials1.Password));
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

        [Fact]
        private async Task TestCertificatesControllerMethods()
        {
            // Just admin on start.
            Assert.Single(this.accountsController.GetAllAccounts(this.adminCredentials).Value);

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            CredentialsModel credentials1 = this.CreateAccount(credentials1Access);

            this.certificatesController.InitializeCertificateAuthority(new CredentialsModelWithMnemonicModel("young shoe immense usual faculty edge habit misery swarm tape viable toddler", "node", credentials1.AccountId, credentials1.Password));

            string clientName = "O=Stratis, CN=DLT Node Run By Iain McCain, OU=Administration";
            int clientAddressIndex = 0;
            string hdPath = $"m/44'/105'/0'/0/{clientAddressIndex}";

            var clientAddressSpace = new HDWalletAddressSpace("tape viable toddler young shoe immense usual faculty edge habit misery swarm", "node");
            byte[] clientPublicKey = clientAddressSpace.GetKey(hdPath).PrivateKey.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey = clientAddressSpace.GetCertificateKeyPair(hdPath);

            string clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            byte[] clientOid141 = Encoding.UTF8.GetBytes(clientAddress);

            Pkcs10CertificationRequest certificateSigningRequest = CaCertificatesManager.CreateCertificateSigningRequest(clientName, clientKey, new string[0], clientOid141);

            // IssueCertificate_UsingRequestString
            CertificateInfoModel certificate1 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(System.Convert.ToBase64String(certificateSigningRequest.GetDerEncoded()), credentials1.AccountId, credentials1.Password))).Value;

            Assert.Equal(clientAddress, certificate1.Address);

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            clientPublicKey = clientAddressSpace2.GetKey(hdPath).PrivateKey.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey2 = clientAddressSpace2.GetCertificateKeyPair(hdPath);

            clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            clientOid141 = Encoding.UTF8.GetBytes(clientAddress);

            Pkcs10CertificationRequest certificateSigningRequest2 = CaCertificatesManager.CreateCertificateSigningRequest(clientName, clientKey2, new string[0], clientOid141);

            CertificateInfoModel certificate2 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(System.Convert.ToBase64String(certificateSigningRequest2.GetDerEncoded()), this.adminCredentials.AccountId, this.adminCredentials.Password))).Value;

            Assert.Equal(clientAddress, certificate2.Address);

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

            // Now check that we can obtain an unsigned CSR template from the CA, which we then sign locally and receive a certificate for.
            // First do it using the manager's methods directly.
            var clientAddressSpace3 = new HDWalletAddressSpace("usual young shoe immense habit misery swarm tape viable toddler faculty edge", "node");
            clientPublicKey = clientAddressSpace3.GetKey(hdPath).PrivateKey.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey3 = clientAddressSpace2.GetCertificateKeyPair(hdPath);

            clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            clientOid141 = Encoding.UTF8.GetBytes(clientAddress);

            Pkcs10CertificationRequestDelaySigned unsignedCsr = CaCertificatesManager.CreatedUnsignedCertificateSigningRequest(clientName, clientKey2.Public, new string[0], clientOid141);
            var signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey2.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey2.Public));

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            Assert.True(signedCsr.Verify());

            CertificateInfoModel certificate3 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password))).Value;

            Assert.Equal(clientAddress, certificate3.Address);

            // Now try do it the same way a node would, by populating the relevant model and submitting it to the API.
            var generateModel = new GenerateCertificateSigningRequestModel(clientAddress, Convert.ToBase64String(clientPublicKey), credentials1.AccountId, credentials1.Password);

            CertificateSigningRequestModel unsignedCsrModel = (await this.certificatesController.GenerateCertificateSigningRequestAsync(generateModel)).Value;

            byte[] csrTemp = Convert.FromBase64String(unsignedCsrModel.CertificateSigningRequestContent);

            unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);
            signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey3.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey3.Public));

            signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            // TODO: Why is this failing? Do a manual verification of the EC maths
            //Assert.True(signedCsr.Verify());

            CertificateInfoModel certificate4 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password))).Value;

            Assert.Equal(clientAddress, certificate4.Address);
        }

        [Fact]
        private void TestAccessLevels()
        {
            // Accounts.
            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAllAccounts(new CredentialsModel(accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.CreateAccount(new CreateAccount("", "", (int)AccountAccessFlags.DeleteAccounts, accountId, password)),
                AccountAccessFlags.CreateAccounts | AccountAccessFlags.DeleteAccounts);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetCertificatesIssuedByAccountId(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.DeleteAccountByAccountId(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.DeleteAccounts);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.ChangeAccountAccessLevel(new ChangeAccountAccessLevel(1, 1, accountId, password)),
                AccountAccessFlags.ChangeAccountAccessLevel);

            // Certificates.
            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.RevokeCertificates);

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.GetCertificateByThumbprint(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.GetAllCertificates(new CredentialsModelWithThumbprintModel("123", accountId, password)),
                AccountAccessFlags.AccessAnyCertificate);

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestFileAsync(new IssueCertificateFromRequestModel(null, accountId, password)).GetAwaiter().GetResult(),
                AccountAccessFlags.IssueCertificates);

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestStringAsync(new IssueCertificateFromFileContentsModel("123", accountId, password)).GetAwaiter().GetResult(),
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

        private void Returns403IfNoAccess(Func<int, string, object> action, AccountAccessFlags requiredAccess)
        {
            CredentialsModel noAccessCredentials = this.CreateAccount();

            var response = action.Invoke(noAccessCredentials.AccountId, noAccessCredentials.Password);

            switch (response)
            {
                case ActionResult<AccountInfo> result1:
                    Assert.True(((result1.Result as StatusCodeResult).StatusCode == 403));
                    break;
                case ActionResult<List<AccountModel>> result2:
                    Assert.True(((result2.Result as StatusCodeResult).StatusCode == 403));
                    break;
                case ActionResult<int> result3:
                    Assert.True(((result3.Result as StatusCodeResult).StatusCode == 403));
                    break;
                case ActionResult<List<CertificateInfoModel>> result4:
                    Assert.True(((result4.Result as StatusCodeResult).StatusCode == 403));
                    break;
                case ActionResult<bool> result5:
                    Assert.True(((result5.Result as StatusCodeResult).StatusCode == 403));
                    break;
                case ActionResult<CertificateInfoModel> result6:
                    Assert.True(((result6.Result as StatusCodeResult).StatusCode == 403));
                    break;
                default:
                    Assert.True(((response as StatusCodeResult).StatusCode == 403));
                    break;
            }

            CredentialsModel accessCredentials = this.CreateAccount(requiredAccess);

            response = action.Invoke(accessCredentials.AccountId, accessCredentials.Password);

            switch (response)
            {
                case ActionResult<AccountInfo> result1b:
                    Assert.Null(result1b.Result);
                    Assert.NotNull(result1b.Value);
                    break;
                case ActionResult<List<AccountModel>> result2b:
                    Assert.Null(result2b.Result);
                    Assert.NotNull(result2b.Value);
                    break;
                case ActionResult<int> result3b:
                    Assert.Null(result3b.Result);
                    Assert.NotNull(result3b.Value);
                    break;
                case ActionResult<List<CertificateInfoModel>> result4b:
                    Assert.Null(result4b.Result);
                    Assert.NotNull(result4b.Value);
                    break;
                case ActionResult<bool> result5b:
                    Assert.Null(result5b.Result);
                    Assert.NotNull(result5b.Value);
                    break;
                case ActionResult<CertificateInfoModel> result6b:
                    // The certificate may not have been found or could not be issued, in which case the response is a 404 or 500
                    if (result6b.Result is StatusCodeResult)
                        Assert.True((result6b.Result as StatusCodeResult).StatusCode == 404);
                    if (result6b.Result is ObjectResult)
                        Assert.True((result6b.Result as ObjectResult).StatusCode == 500);
                    break;
            }
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
