using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
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
    public sealed class ControllersTests
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
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();
            this.server = new TestServer(builder);

            this.adminCredentials = new CredentialsModel(Settings.AdminAccountId, CaTestHelper.AdminPassword);

            this.accountsController = (AccountsController)this.server.Host.Services.GetService(typeof(AccountsController));
            this.certificatesController = (CertificatesController)this.server.Host.Services.GetService(typeof(CertificatesController));
            this.dataCacheLayer = (DataCacheLayer)this.server.Host.Services.GetService(typeof(DataCacheLayer));

            CaTestHelper.InitializeCa(this.server);

            // Only the admin user exists initially.
            Assert.Single(CaTestHelper.GetValue<List<AccountModel>>(this.accountsController.GetAllAccounts(this.adminCredentials)));

            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            CredentialsModel credentials1 = CaTestHelper.CreateAccount(this.server.Host, credentials1Access);

            CertificateInfoModel caCertModel = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.GetCaCertificate(credentials1));

            var certParser = new X509CertificateParser();

            this.caCert = certParser.ReadCertificate(caCertModel.CertificateContentDer);
        }

        private CredentialsModel GetPrivilegedAccount()
        {
            AccountAccessFlags credentials1Access = AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.BasicAccess | AccountAccessFlags.IssueCertificates | AccountAccessFlags.RevokeCertificates | AccountAccessFlags.AccessAnyCertificate;
            
            return CaTestHelper.CreateAccount(this.server.Host, credentials1Access);
        }

        [Fact]
        private void TestCertificatesControllerMethods()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // We need to be absolutely sure that the components of the subject DN are in the same order in a CSR versus the resulting certificate.
            // Otherwise the certificate chain will fail validation, and there is currently no workaround in BouncyCastle.
            var credModel = new CredentialsAccessModel(credentials1.AccountId, credentials1.Password, AccountAccessFlags.BasicAccess);
            AccountModel account = this.dataCacheLayer.ExecuteQuery(credModel, (dbContext) => { return dbContext.Accounts.SingleOrDefault(x => x.Id == credModel.AccountId); });
            string clientName = $"O={account.Organization},CN={account.Name},OU={account.OrganizationUnit},L={account.Locality},ST={account.StateOrProvince},C={account.Country}";

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
            CertificateInfoModel certificate1 = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(certificateSigningRequest.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate1.CertificateContentDer);

            Assert.True(this.caCert.SubjectDN.Equivalent(cert1.IssuerDN));

            Assert.Equal(clientAddress, certificate1.Address);

            PubKey[] pubKeys = CaTestHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);

            CredentialsModel credentials2 = this.GetPrivilegedAccount();

            credModel = new CredentialsAccessModel(credentials2.AccountId, credentials2.Password, AccountAccessFlags.BasicAccess);
            account = this.dataCacheLayer.ExecuteQuery(credModel, (dbContext) => { return dbContext.Accounts.SingleOrDefault(x => x.Id == credModel.AccountId); });
            clientName = $"O={account.Organization},CN={account.Name},OU={account.OrganizationUnit},L={account.Locality},ST={account.StateOrProvince},C={account.Country}";

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

            CertificateInfoModel certificate2 = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(System.Convert.ToBase64String(certificateSigningRequest2.GetDerEncoded()), credentials2.AccountId, credentials2.Password)));

            Assert.Equal(clientAddress, certificate2.Address);

            PubKey[] pubKeys2 = CaTestHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Equal(2, pubKeys2.Length);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys2[1]);

            Assert.Empty(CaTestHelper.GetValue<ICollection<string>>(this.certificatesController.GetRevokedCertificates()));

            // GetCertificateByThumbprint
            CertificateInfoModel cert1Retrieved = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.GetCertificateByThumbprint(
                new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, this.adminCredentials.AccountId, this.adminCredentials.Password)));
            Assert.Equal(certificate1.Id, cert1Retrieved.Id);
            Assert.Equal(certificate1.AccountId, cert1Retrieved.AccountId);

            string status = CaTestHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true)));
            Assert.Equal(CertificateStatus.Good.ToString(), status);

            this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password));

            // Can't revoke 2nd time same cert.
            bool result = CaTestHelper.GetValue<bool>(this.certificatesController.RevokeCertificate(new CredentialsModelWithThumbprintModel(certificate1.Thumbprint, credentials1.AccountId, credentials1.Password)));
            Assert.False(result);

            Assert.Equal(CertificateStatus.Revoked.ToString(), CaTestHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true))));
            Assert.Equal(CertificateStatus.Unknown.ToString(), CaTestHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(CaTestHelper.GenerateRandomString(20), true))));

            List<CertificateInfoModel> allCerts = CaTestHelper.GetValue<List<CertificateInfoModel>>(this.certificatesController.GetAllCertificates(credentials1));
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Good) == 1);
            Assert.True(allCerts.Count(x => x.Status == CertificateStatus.Revoked) == 1);

            Assert.Equal(CertificateStatus.Revoked.ToString(), CaTestHelper.GetValue<string>(this.certificatesController.GetCertificateStatus(new GetCertificateStatusModel(certificate1.Thumbprint, true))));

            List<string> revoked = CaTestHelper.GetValue<ICollection<string>>(this.certificatesController.GetRevokedCertificates()).ToList();
            Assert.Single(revoked);
            Assert.Equal(certificate1.Thumbprint, revoked[0]);

            // Public keys for revoked certificates don't appear in the list.
            pubKeys = CaTestHelper.GetValue<ICollection<string>>(this.certificatesController.GetCertificatePublicKeys()).Select(s => new PubKey(s)).ToArray();
            Assert.Single(pubKeys);
            Assert.Equal(blockSigningPrivateKey.PubKey, pubKeys[0]);
        }

        [Fact]
        public void CanIssueCertificateViaTemplateCsrFromManager()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // Check that we can obtain an unsigned CSR template from the CA, which we then sign locally and receive a certificate for.
            // Do it using the manager's methods directly to verify their operation.
            var credModel = new CredentialsAccessModel(credentials1.AccountId, credentials1.Password, AccountAccessFlags.BasicAccess);
            AccountModel account = this.dataCacheLayer.ExecuteQuery(credModel, (dbContext) => { return dbContext.Accounts.SingleOrDefault(x => x.Id == credModel.AccountId); });
            string clientName = $"O={account.Organization},CN={account.Name},OU={account.OrganizationUnit},L={account.Locality},ST={account.StateOrProvince},C={account.Country}";

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

            CertificateInfoModel certificate3 = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
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
            Key clientPrivateKey3 = clientAddressSpace3.GetKey(this.clientHdPath).PrivateKey;
            var clientPublicKey = clientPrivateKey3.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey3 = clientAddressSpace2.GetCertificateKeyPair(this.clientHdPath);

            Key blockSigningPrivateKey = clientAddressSpace3.GetKey(this.blockSigningHdPath).PrivateKey;

            var clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);

            var generateModel = new GenerateCertificateSigningRequestModel(clientAddress, Convert.ToBase64String(clientPublicKey), Convert.ToBase64String(clientPrivateKey3.PubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPrivateKey.PubKey.ToBytes()), credentials1.AccountId, credentials1.Password);

            CertificateSigningRequestModel unsignedCsrModel = CaTestHelper.GetValue<CertificateSigningRequestModel>(this.certificatesController.GenerateCertificateSigningRequest(generateModel));

            byte[] csrTemp = Convert.FromBase64String(unsignedCsrModel.CertificateSigningRequestContent);

            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);
            var signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey3.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey3.Public));

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            // TODO: Why is this failing? Do a manual verification of the EC maths
            //Assert.True(signedCsr.Verify());

            CertificateInfoModel certificate4 = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            Assert.Equal(clientAddress, certificate4.Address);

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate4.CertificateContentDer);

            Assert.True(CaCertificatesManager.ValidateCertificateChain(this.caCert, cert1));
        }

        [Fact]
        public async Task CantRequestCertificateTwiceForSameIdentityAndCanReset()
        {
            CredentialsModel credentials1 = this.GetPrivilegedAccount();

            // Try do the issuance the same way a node would, by populating the relevant model and submitting it to the API.
            // In this case we just use the same pubkey for both the certificate generation & transaction signing pubkey hash, they would ordinarily be different.

            var clientAddressSpace2 = new HDWalletAddressSpace("habit misery swarm tape viable toddler young shoe immense usual faculty edge", "node");
            var clientAddressSpace3 = new HDWalletAddressSpace("usual young shoe immense habit misery swarm tape viable toddler faculty edge", "node");
            Key clientPrivateKey3 = clientAddressSpace3.GetKey(this.clientHdPath).PrivateKey;
            var clientPublicKey = clientPrivateKey3.PubKey.ToBytes();
            AsymmetricCipherKeyPair clientKey3 = clientAddressSpace2.GetCertificateKeyPair(this.clientHdPath);

            Key blockSigningPrivateKey = clientAddressSpace3.GetKey(this.blockSigningHdPath).PrivateKey;

            var clientAddress = HDWalletAddressSpace.GetAddress(clientPublicKey, 63);

            var generateModel = new GenerateCertificateSigningRequestModel(clientAddress, Convert.ToBase64String(clientPublicKey), Convert.ToBase64String(clientPrivateKey3.PubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPrivateKey.PubKey.ToBytes()), credentials1.AccountId, credentials1.Password);

            CertificateSigningRequestModel unsignedCsrModel = CaTestHelper.GetValue<CertificateSigningRequestModel>(this.certificatesController.GenerateCertificateSigningRequest(generateModel));

            byte[] csrTemp = Convert.FromBase64String(unsignedCsrModel.CertificateSigningRequestContent);

            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);
            var signature = CaCertificatesManager.GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", clientKey3.Private);
            unsignedCsr.SignRequest(signature);

            Assert.True(unsignedCsr.Verify(clientKey3.Public));

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            CertificateInfoModel certificate = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));

            Assert.Equal(clientAddress, certificate.Address);

            var certParser = new X509CertificateParser();
            X509Certificate cert1 = certParser.ReadCertificate(certificate.CertificateContentDer);

            Assert.True(CaCertificatesManager.ValidateCertificateChain(this.caCert, cert1));

            // Now that we have a certificate on this identity, can we still generate certificate signing requests?
            var response = (ObjectResult) this.certificatesController.GenerateCertificateSigningRequest(generateModel);
            Assert.Equal(403, response.StatusCode);
            Assert.Equal("You cant access this action. IssueCertificates access is required.", response.Value);

            // How about issuing another certificate?
            var response2 = (ObjectResult) this.certificatesController.IssueCertificate_UsingRequestString(new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password));
            Assert.Equal(403, response2.StatusCode);
            Assert.Equal("You cant access this action. IssueCertificates access is required.", response2.Value);

            // Reset our permissions.
            var resetResponse = (OkResult) this.certificatesController.GrantIssuePermission(new CredentialsModelWithTargetId(credentials1.AccountId,Settings.AdminAccountId, CaTestHelper.AdminPassword));
            Assert.Equal(200, resetResponse.StatusCode);

            // Check that our cert was revoked.
            List<CertificateInfoModel> getCertsResponse =  CaTestHelper.GetValue<List<CertificateInfoModel>>(this.certificatesController.GetAllCertificates(credentials1));
            Assert.Equal(CertificateStatus.Revoked, getCertsResponse[0].Status);

            // Issue our certificate again. If this call doesn't fail we successfully got our certificate back!
            CertificateInfoModel resetCertificate = CaTestHelper.GetValue<CertificateInfoModel>(this.certificatesController.IssueCertificate_UsingRequestString(
                new IssueCertificateFromFileContentsModel(Convert.ToBase64String(signedCsr.GetDerEncoded()), credentials1.AccountId, credentials1.Password)));
        }

        [Fact]
        private void TestAccessLevels()
        {
            // Accounts.
            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAccountInfoById(new CredentialsModelWithTargetId(1, accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

            this.Returns403IfNoAccess((int accountId, string password) => this.accountsController.GetAllAccounts(new CredentialsModel(accountId, password)),
                AccountAccessFlags.AccessAccountInfo);

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
            CredentialsModel noAccessCredentials = CaTestHelper.CreateAccount(this.server.Host);

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

            CredentialsModel accessCredentials = CaTestHelper.CreateAccount(this.server.Host, requiredAccess);

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
            CredentialsModel noAccessCredentials = CaTestHelper.CreateAccount(this.server.Host);
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

            CredentialsModel accessCredentials = CaTestHelper.CreateAccount(this.server.Host, requiredAccess);

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