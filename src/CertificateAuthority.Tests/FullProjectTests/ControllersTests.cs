﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.FullProjectTests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Xunit;
using AccountAccessFlags = CertificateAuthority.Models.AccountAccessFlags;
using AccountInfo = CertificateAuthority.Models.AccountInfo;
using CertificateInfoModel = CertificateAuthority.Models.CertificateInfoModel;
using CertificateStatus = CertificateAuthority.Models.CertificateStatus;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public class ControllersTests
    {
        private readonly AccountsController accountsController;
        private CredentialsModel adminCredentials;
        private readonly CertificatesController certificatesController;
        private readonly DataCacheLayer dataCacheLayer;

        public ControllersTests()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();
            var server = new TestServer(builder);

            this.adminCredentials = new CredentialsModel(1, "4815162342");

            this.accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));
            this.certificatesController = (CertificatesController)server.Host.Services.GetService(typeof(CertificatesController));
            this.dataCacheLayer = (DataCacheLayer)server.Host.Services.GetService(typeof(DataCacheLayer));
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
        public async Task TestCertificatesControllerMethodsAsync()
        {
            // Just admin on start.
            Assert.Single(this.accountsController.GetAllAccounts(this.adminCredentials).Value);

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            CredentialsModel credentials1 = this.CreateAccount(credentials1Access);

            this.certificatesController.InitializeCertificateAuthority(new CredentialsModelWithMnemonicModel("young shoe immense usual faculty edge habit misery swarm tape viable toddler", "node", 105, 63, credentials1.AccountId, credentials1.Password));

            var caCertModel = this.certificatesController.GetCaCertificate(credentials1).Value;

            var certParser = new X509CertificateParser();

            X509Certificate caCert = certParser.ReadCertificate(Convert.FromBase64String(caCertModel.CertificateContentDer));

            Assert.NotNull(caCert);

            // We need to be absolutely sure that the components of the subject DN are in the same order in a CSR versus the resulting certificate.
            // Otherwise the certificate chain will fail validation, and there is currently no workaround in BouncyCastle.
            string clientName = "O=Stratis,CN=DLT Node Run By Iain McCain,OU=Administration";

            var clientAddressSpace = new HDWalletAddressSpace("tape viable toddler young shoe immense usual faculty edge habit misery swarm", "node");

            int transactionSigningIndex = 0;
            int blockSigningIndex = 1;
            int clientAddressIndex = 2;
            
            string clientHdPath = $"m/44'/105'/0'/0/{clientAddressIndex}";
            string transactionSigningHdPath = $"m/44'/105'/0'/0/{transactionSigningIndex}";
            string blockSigningHdPath = $"m/44'/105'/0'/0/{blockSigningIndex}";

            Key clientPrivateKey = clientAddressSpace.GetKey(clientHdPath).PrivateKey;
            Key transactionSigningPrivateKey = clientAddressSpace.GetKey(transactionSigningHdPath).PrivateKey;
            Key blockSigningPrivateKey = clientAddressSpace.GetKey(blockSigningHdPath).PrivateKey;

            AsymmetricCipherKeyPair clientKey = clientAddressSpace.GetCertificateKeyPair(clientHdPath);

            string clientAddress = HDWalletAddressSpace.GetAddress(clientPrivateKey.PubKey.ToBytes(), 63);
            byte[] clientOid141 = Encoding.UTF8.GetBytes(clientAddress);
            byte[] clientOid142 = transactionSigningPrivateKey.PubKey.Hash.ToBytes();
            byte[] clientOid143 = blockSigningPrivateKey.PubKey.ToBytes();

            var extensionData = new Dictionary<string, byte[]>
            {
                {CaCertificatesManager.P2pkhExtensionOid, clientOid141},
                {CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid, clientOid142},
                {CaCertificatesManager.BlockSigningPubKeyExtensionOid, clientOid143}
            };

            Pkcs10CertificationRequest certificateSigningRequest = CaCertificatesManager.CreateCertificateSigningRequest(clientName, clientKey, new string[0], extensionData);

            // IssueCertificate_UsingRequestString
            CertificateInfoModel certificate1 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(certificateSigningRequest.GetDerEncoded()), credentials1.AccountId, credentials1.Password))).Value;

            X509Certificate cert1 = certParser.ReadCertificate(Convert.FromBase64String(certificate1.CertificateContentDer));

            Assert.True(caCert.SubjectDN.Equivalent(cert1.IssuerDN));

            Assert.Equal(clientAddress, certificate1.Address);

            PubKey[] pubKeys = this.certificatesController.GetCertificatePublicKeys().Value.ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            Key clientPrivateKey2 = clientAddressSpace2.GetKey(clientHdPath).PrivateKey;
            byte[] clientPublicKey = clientPrivateKey2.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey2 = clientAddressSpace2.GetCertificateKeyPair(clientHdPath);

            blockSigningPrivateKey = clientAddressSpace2.GetKey(blockSigningHdPath).PrivateKey;

            clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            clientOid141 = Encoding.UTF8.GetBytes(clientAddress);
            clientOid142 = clientPublicKey;
            clientOid143 = blockSigningPrivateKey.PubKey.ToBytes();

            extensionData = new Dictionary<string, byte[]>
            {
                {CaCertificatesManager.P2pkhExtensionOid, clientOid141},
                {CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid, clientOid142},
                {CaCertificatesManager.BlockSigningPubKeyExtensionOid, clientOid143},
                {CaCertificatesManager.SendPermission, new byte[] { 1 } },
                {CaCertificatesManager.CallContractPermissionOid, new byte[] { 1 } },
                {CaCertificatesManager.CreateContractPermissionOid, new byte[] { 1 } }
            };

            Pkcs10CertificationRequest certificateSigningRequest2 = CaCertificatesManager.CreateCertificateSigningRequest(clientName, clientKey2, new string[0], extensionData);

            CertificateInfoModel certificate2 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(System.Convert.ToBase64String(certificateSigningRequest2.GetDerEncoded()), this.adminCredentials.AccountId, this.adminCredentials.Password))).Value;

            Assert.Equal(clientAddress, certificate2.Address);

            PubKey[] pubKeys2 = this.certificatesController.GetCertificatePublicKeys().Value.ToArray();
            Assert.Equal(2, pubKeys2.Length);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys2[1]);

            Assert.Empty(this.certificatesController.GetRevokedCertificates().Value);

            // GetCertificateByThumbprint
            CertificateInfoModel cert1Retrieved = this.certificatesController.GetCertificateForThumbprint(
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

            // Public keys for revoked certificates don't appear in the list.
            pubKeys = this.certificatesController.GetCertificatePublicKeys().Value.ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);

            // Now check that we can obtain an unsigned CSR template from the CA, which we then sign locally and receive a certificate for.
            // First do it using the manager's methods directly.
            var clientAddressSpace3 = new HDWalletAddressSpace("usual young shoe immense habit misery swarm tape viable toddler faculty edge", "node");
            Key clientPrivateKey3 = clientAddressSpace3.GetKey(clientHdPath).PrivateKey;
            clientPublicKey = clientPrivateKey3.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey3 = clientAddressSpace2.GetCertificateKeyPair(clientHdPath);

            blockSigningPrivateKey = clientAddressSpace2.GetKey(blockSigningHdPath).PrivateKey;

            clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            clientOid141 = Encoding.UTF8.GetBytes(clientAddress);
            clientOid142 = clientPublicKey;
            clientOid143 = blockSigningPrivateKey.PubKey.ToBytes();

            extensionData = new Dictionary<string, byte[]>
            {
                {CaCertificatesManager.P2pkhExtensionOid, clientOid141},
                {CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid, clientOid142},
                {CaCertificatesManager.BlockSigningPubKeyExtensionOid, clientOid143}
            };

            Pkcs10CertificationRequestDelaySigned unsignedCsr = CaCertificatesManager.CreatedUnsignedCertificateSigningRequest(clientName, clientKey2.Public, new string[0], extensionData);
            var signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey2.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey2.Public));

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            Assert.True(signedCsr.Verify());

            CertificateInfoModel certificate3 = (await this.certificatesController.IssueCertificate_UsingRequestStringAsync(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password))).Value;

            Assert.Equal(clientAddress, certificate3.Address);

            // Now try do it the same way a node would, by populating the relevant model and submitting it to the API.
            // In this case we just use the same pubkey for both the certificate generation & transaction signing pubkey hash, they would ordinarily be different.
            var generateModel = new GenerateCertificateSigningRequestModel(clientAddress, Convert.ToBase64String(clientPublicKey), Convert.ToBase64String(clientPrivateKey.PubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPrivateKey.PubKey.ToBytes()), credentials1.AccountId, credentials1.Password);

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

            Assert.True(CaCertificatesManager.ValidateCertificateChain(caCert, cert1));
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

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.GetCertificateForThumbprint(new CredentialsModelWithThumbprintModel("123", accountId, password)),
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
