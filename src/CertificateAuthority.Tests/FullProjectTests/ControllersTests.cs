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
        private readonly CredentialsModel adminCredentials;
        private readonly CertificatesController certificatesController;
        private readonly DataCacheLayer dataCacheLayer;
        private readonly TestServer server;

        private static int transactionSigningIndex = 0;
        private static int blockSigningIndex = 1;
        private static int clientAddressIndex = 2;

        private readonly string clientHdPath = $"m/44'/105'/0'/0/{clientAddressIndex}";
        private readonly string transactionSigningHdPath = $"m/44'/105'/0'/0/{transactionSigningIndex}";
        private readonly string blockSigningHdPath = $"m/44'/105'/0'/0/{blockSigningIndex}";

        private X509Certificate caCert;

        public ControllersTests()
        {
            IWebHostBuilder builder = TestsHelper.CreateWebHostBuilder();
            this.server = new TestServer(builder);

            this.adminCredentials = new CredentialsModel(1, "4815162342");

            this.accountsController = (AccountsController)this.server.Host.Services.GetService(typeof(AccountsController));
            this.certificatesController = (CertificatesController)this.server.Host.Services.GetService(typeof(CertificatesController));
            this.dataCacheLayer = (DataCacheLayer)this.server.Host.Services.GetService(typeof(DataCacheLayer));

            this.certificatesController.InitializeCertificateAuthority(new CredentialsModelWithMnemonicModel("young shoe immense usual faculty edge habit misery swarm tape viable toddler", "node", 105, 63, this.adminCredentials.AccountId, this.adminCredentials.Password));

            CertificateInfoModel caCertModel = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.GetCaCertificate(this.adminCredentials));

            var certParser = new X509CertificateParser();

            this.caCert = certParser.ReadCertificate(caCertModel.CertificateContentDer);
        }

        private CredentialsModel GetPrivilegedAccount()
        {
            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            
            return TestsHelper.CreateAccount(this.server, credentials1Access);
        }

        [Fact]
        private async Task TestCertificatesControllerMethodsAsync()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // We need to be absolutely sure that the components of the subject DN are in the same order in a CSR versus the resulting certificate.
            // Otherwise the certificate chain will fail validation, and there is currently no workaround in BouncyCastle.
            string clientName = "O=Stratis,CN=DLT Node Run By Iain McCain,OU=Administration";

            var clientAddressSpace = new HDWalletAddressSpace("tape viable toddler young shoe immense usual faculty edge habit misery swarm", "node");

            Key clientPrivateKey = clientAddressSpace.GetKey(this.clientHdPath).PrivateKey;
            Key transactionSigningPrivateKey = clientAddressSpace.GetKey(this.transactionSigningHdPath).PrivateKey;
            Key blockSigningPrivateKey = clientAddressSpace.GetKey(this.blockSigningHdPath).PrivateKey;

            AsymmetricCipherKeyPair clientKey = clientAddressSpace.GetCertificateKeyPair(this.clientHdPath);

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
            CertificateInfoModel certificate1 = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(certificateSigningRequest.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate1.CertificateContentDer);

            Assert.True(caCert.SubjectDN.Equivalent(cert1.IssuerDN));

            Assert.Equal(clientAddress, certificate1.Address);

            PubKey[] pubKeys = TestsHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            Key clientPrivateKey2 = clientAddressSpace2.GetKey(this.clientHdPath).PrivateKey;
            byte[] clientPublicKey = clientPrivateKey2.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey2 = clientAddressSpace2.GetCertificateKeyPair(this.clientHdPath);

            blockSigningPrivateKey = clientAddressSpace2.GetKey(this.blockSigningHdPath).PrivateKey;

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

            CertificateInfoModel certificate2 = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(System.Convert.ToBase64String(certificateSigningRequest2.GetDerEncoded()), this.adminCredentials.AccountId, this.adminCredentials.Password)));

            Assert.Equal(clientAddress, certificate2.Address);

            PubKey[] pubKeys2 = TestsHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Equal(2, pubKeys2.Length);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys2[1]);

            Assert.Empty(TestsHelper.GetValue<ICollection<string>>(this.certificatesController.GetRevokedCertificates()));

            // GetCertificateByThumbprint
            CertificateInfoModel cert1Retrieved = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.GetCertificateByThumbprint(
                new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, this.adminCredentials.AccountId, this.adminCredentials.Password)));
            Assert.Equal(certificate1.Id, cert1Retrieved.Id);
            Assert.Equal(certificate1.AccountId, cert1Retrieved.AccountId);

            string status = TestsHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true)));
            Assert.Equal(CertificateStatus.Good.ToString(), status);

            this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password));

            // Can't revoke 2nd time same cert.
            bool result = TestsHelper.GetValue<bool>(this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password)));
            Assert.False(result);

            Assert.Equal(CertificateStatus.Revoked.ToString(), TestsHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true))));
            Assert.Equal(CertificateStatus.Unknown.ToString(), TestsHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(TestsHelper.GenerateRandomString(20), true))));

            List<CertificateInfoModel> allCerts = TestsHelper.GetValue<List<CertificateInfoModel>>(this.certificatesController.GetAllCertificates(credentials1));
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Good) == 1);
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Revoked) == 1);

            Assert.Equal(CertificateStatus.Revoked.ToString(), TestsHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true))));

            List<string> revoked = TestsHelper.GetValue<ICollection<string>>(this.certificatesController.GetRevokedCertificates()).ToList();
            Assert.Single(revoked);
            Assert.Equal(certificate1.Thumbprint, revoked[0]);

            // Public keys for revoked certificates don't appear in the list.
            pubKeys = TestsHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);
        }

        [Fact]
        public void CanIssueCertificateViaTemplateCsrFromManager()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // Check that we can obtain an unsigned CSR template from the CA, which we then sign locally and receive a certificate for.
            // Do it using the manager's methods directly to verify their operation.

            string clientName = "O=Stratis,CN=DLT Node Run By Iain McCain,OU=Administration";

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            var clientAddressSpace3 = new HDWalletAddressSpace("usual young shoe immense habit misery swarm tape viable toddler faculty edge", "node");
            Key clientPrivateKey3 = clientAddressSpace3.GetKey(this.clientHdPath).PrivateKey;
            var clientPublicKey = clientPrivateKey3.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey2 = clientAddressSpace2.GetCertificateKeyPair(this.clientHdPath);

            Key blockSigningPrivateKey = clientAddressSpace2.GetKey(this.blockSigningHdPath).PrivateKey;

            var clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);
            var clientOid141 = Encoding.UTF8.GetBytes(clientAddress);
            var clientOid142 = clientPublicKey;
            var clientOid143 = blockSigningPrivateKey.PubKey.ToBytes();

            var extensionData = new Dictionary<string, byte[]>
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

            CertificateInfoModel certificate3 = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            Assert.Equal(clientAddress, certificate3.Address);

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate3.CertificateContentDer);

            Assert.True(CaCertificatesManager.ValidateCertificateChain(this.caCert, cert1));
        }

        [Fact]
        public void CanIssueCertificateViaTemplateCsr()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // Try do the issuance the same way a node would, by populating the relevant model and submitting it to the API.
            // In this case we just use the same pubkey for both the certificate generation & transaction signing pubkey hash, they would ordinarily be different.

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            var clientAddressSpace3 = new HDWalletAddressSpace("usual young shoe immense habit misery swarm tape viable toddler faculty edge", "node");
            Key clientPrivateKey3 = clientAddressSpace3.GetKey(clientHdPath).PrivateKey;
            var clientPublicKey = clientPrivateKey3.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey3 = clientAddressSpace2.GetCertificateKeyPair(clientHdPath);

            Key blockSigningPrivateKey = clientAddressSpace3.GetKey(blockSigningHdPath).PrivateKey;

            var clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);

            var generateModel = new GenerateCertificateSigningRequestModel(clientAddress, Convert.ToBase64String(clientPublicKey), Convert.ToBase64String(clientPrivateKey3.PubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPrivateKey.PubKey.ToBytes()), credentials1.AccountId, credentials1.Password);

            CertificateSigningRequestModel unsignedCsrModel = TestsHelper.GetValue<CertificateSigningRequestModel>(this.certificatesController.GenerateCertificateSigningRequest(generateModel));

            byte[] csrTemp = Convert.FromBase64String(unsignedCsrModel.CertificateSigningRequestContent);

            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);
            var signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey3.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey3.Public));

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            // TODO: Why is this failing? Do a manual verification of the EC maths
            //Assert.True(signedCsr.Verify());

            CertificateInfoModel certificate4 = TestsHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            Assert.Equal(clientAddress, certificate4.Address);

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate4.CertificateContentDer);

            Assert.True(CaCertificatesManager.ValidateCertificateChain(this.caCert, cert1));
        }

        [Fact]
        private void TestAccessLevels()
        {
            // Accounts.
            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAllAccounts(new CredentialsModel(accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.CreateAccount(new CreateAccount("", "", (int)AccountAccessFlags.DeleteAccounts, 
                "dummyOrganizationUnit",
                "dummyOrganization",
                "dummyLocality",
                "dummyStateOrProvince",
                "dummyEmailAddress",
                "dummyCountry",
                new List<string>() { AccountsController.SendPermission }, accountId, password)), AccountAccessFlags.CreateAccounts | AccountAccessFlags.DeleteAccounts);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetCertificateIssuedByAccountId(new CredentialsModelWithTargetId(1, accountId, password)),
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

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestFile(new IssueCertificateFromRequestModel(null, accountId, password)),
                AccountAccessFlags.IssueCertificates);

            this.Returns403IfNoAccess((int accountId, string password) => this.certificatesController.IssueCertificate_UsingRequestString(new IssueCertificateFromFileContentsModel("123", accountId, password)),
                AccountAccessFlags.IssueCertificates);
        }

        private void Returns403IfNoAccess(Func<int, string, object> action, AccountAccessFlags requiredAccess)
        {
            CredentialsModel noAccessCredentials = TestsHelper.CreateAccount(this.server);

            var response = action.Invoke(noAccessCredentials.AccountId, noAccessCredentials.Password);

            switch (response)
            {
                case ActionResult<AccountInfo> result1:
                    Assert.True((result1.Result as StatusCodeResult).StatusCode == 403);
                    break;
                case ActionResult<List<AccountModel>> result2:
                    Assert.True((result2.Result as StatusCodeResult).StatusCode == 403);
                    break;
                case ActionResult<int> result3:
                    Assert.True((result3.Result as StatusCodeResult).StatusCode == 403);
                    break;
                case ActionResult<List<CertificateInfoModel>> result4:
                    Assert.True((result4.Result as StatusCodeResult).StatusCode == 403);
                    break;
                case ActionResult<bool> result5:
                    Assert.True((result5.Result as StatusCodeResult).StatusCode == 403);
                    break;
                case ActionResult<CertificateInfoModel> result6:
                    Assert.True((result6.Result as StatusCodeResult).StatusCode == 403);
                    break;
                default:
                    Assert.True((response as ObjectResult).StatusCode == 403);
                    break;
            }

            CredentialsModel accessCredentials = TestsHelper.CreateAccount(this.server, requiredAccess);

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
                    break;
                case ActionResult<List<CertificateInfoModel>> result4b:
                    Assert.Null(result4b.Result);
                    Assert.NotNull(result4b.Value);
                    break;
                case ActionResult<bool> result5b:
                    Assert.Null(result5b.Result);
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
            CredentialsModel noAccessCredentials = TestsHelper.CreateAccount(this.server);
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

            CredentialsModel accessCredentials = TestsHelper.CreateAccount(this.server, requiredAccess);

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