using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CertificateAuthority;
using CertificateAuthority.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Stratis.Features.PoA.ProtocolEncryption
{
    public sealed class CertificatesManager : ICertificatesManager
    {
        /// <summary>Name of authority .crt certificate that is supposed to be found in application folder.</summary>
        /// <remarks>This certificate is automatically copied during the build.</remarks>
        public const string AuthorityCertificateName = "CaCertificate.crt";

        /// <summary>Name of client's .pfx certificate that is supposed to be found in node's folder.</summary>
        public const string ClientCertificateName = "ClientCertificate.pfx";

        /// <summary>The password used to decrypt the PKCS#12 (.pfx) file containing the client certificate and private key.</summary>
        public const string ClientCertificateConfigurationKey = "certificatepassword";

        /// <summary>The account ID ('username') used by the node to query the CA.</summary>
        public const string CaAccountIdKey = "caaccountid";

        /// <summary>The password used by the node to query the CA.</summary>
        public const string CaPasswordKey = "capassword";

        /// <summary>The base url key to be used.</summary>
        public const string CaBaseUrl = "http://localhost:5050";

        /// <summary>The base url to be used to query the CA.</summary>
        public const string CaBaseUrlKey = "caurl";

        /// <inheritdoc/>
        public X509Certificate AuthorityCertificate { get; private set; }

        /// <inheritdoc/>
        public X509Certificate ClientCertificate { get; private set; }

        /// <inheritdoc/>
        public AsymmetricKeyParameter ClientCertificatePrivateKey { get; private set; }

        private readonly DataFolder dataFolder;

        private readonly IRevocationChecker revocationChecker;

        private readonly ILogger logger;

        private readonly Network network;

        private readonly TextFileConfiguration configuration;

        private readonly string caUrl;

        private string caPassword;

        private int caAccountId;

        private string clientCertificatePassword;

        public CertificatesManager(DataFolder dataFolder, NodeSettings nodeSettings, ILoggerFactory loggerFactory, IRevocationChecker revocationChecker, Network network)
        {
            this.dataFolder = dataFolder;
            this.configuration = nodeSettings.ConfigReader;
            this.revocationChecker = revocationChecker;
            this.network = network;

            this.caUrl = this.configuration.GetOrDefault(CaBaseUrlKey, CaBaseUrl);

            this.logger = loggerFactory.CreateLogger(GetType().FullName);
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            this.LoadAuthorityCertificate();
            this.LoadClientCertificate();
        }

        public bool HaveAccount()
        {
            return this.caAccountId != 0 && !string.IsNullOrEmpty(this.caPassword);
        }

        public bool LoadAuthorityCertificate(bool requireAccountId = true)
        {
            string acPath = Path.Combine(this.dataFolder.RootPath, AuthorityCertificateName);

            if (!File.Exists(acPath))
            {
                this.logger.LogTrace("(-)[AC_NOT_FOUND]:{0}='{1}'", nameof(acPath), acPath);
                throw new CertificateConfigurationException($"Authority certificate not located at '{acPath}'. Make sure you place '{AuthorityCertificateName}' in the node's root directory.");
            }

            this.caPassword = this.configuration.GetOrDefault<string>(CaPasswordKey, null);

            if (this.caPassword == null)
            {
                this.logger.LogTrace("(-)[NO_PASSWORD]");
                throw new CertificateConfigurationException($"You have to provide a password for the certificate authority! Use '{CaPasswordKey}' configuration key to provide a password.");
            }

            this.caAccountId = this.configuration.GetOrDefault<int>(CaAccountIdKey, 0);

            if (requireAccountId && this.caAccountId == 0)
            {
                this.logger.LogTrace("(-)[NO_ACCOUNT_ID]");
                throw new CertificateConfigurationException($"You have to provide an account ID for the certificate authority! Use '{CaAccountIdKey}' configuration key to provide an account id.");
            }

            var certParser = new X509CertificateParser();

            this.AuthorityCertificate = certParser.ReadCertificate(File.ReadAllBytes(acPath));

            return true;
        }

        public bool LoadClientCertificate()
        {
            string clientCertPath = Path.Combine(this.dataFolder.RootPath, ClientCertificateName);

            if (!File.Exists(clientCertPath))
            {
                this.logger.LogTrace("(-)[CC_NOT_FOUND]:{0}='{1}'", nameof(clientCertPath), clientCertPath);
                throw new CertificateConfigurationException($"Client certificate not located at '{clientCertPath}'. Make sure you place '{ClientCertificateName}' in the node's root directory.");
            }

            this.clientCertificatePassword = this.configuration.GetOrDefault<string>(ClientCertificateConfigurationKey, null);

            if (this.clientCertificatePassword == null)
            {
                this.logger.LogError("(-)[MISSING_PASSWORD]");
                throw new AuthenticationException($"You have to provide a password for the client certificate! Use '{ClientCertificateConfigurationKey}' configuration key to provide a password.");
            }

            try
            {
                (this.ClientCertificate, this.ClientCertificatePrivateKey) = CaCertificatesManager.LoadPfx(File.ReadAllBytes(clientCertPath), this.clientCertificatePassword);
            }
            catch (IOException)
            {
            }

            if (this.ClientCertificate == null)
            {
                this.logger.LogError("(-)[WRONG_PASSWORD]");
                throw new AuthenticationException($"Client certificate wasn't loaded. Usually this happens when provided password is incorrect.");
            }

            bool clientCertValid = this.IsSignedByAuthorityCertificate(this.ClientCertificate, this.AuthorityCertificate);

            if (!clientCertValid)
                throw new CertificateConfigurationException("Provided client certificate isn't valid or isn't signed by the authority certificate!");

            bool revoked = this.revocationChecker.IsCertificateRevoked(CaCertificatesManager.GetThumbprint(this.ClientCertificate));

            if (revoked)
                throw new CertificateConfigurationException("Provided client certificate was revoked!");

            return true;
        }

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        private bool IsSignedByAuthorityCertificate(X509Certificate certificateToValidate, X509Certificate authorityCertificate)
        {
            return CaCertificatesManager.ValidateCertificateChain(authorityCertificate, certificateToValidate);
        }

        public CaClient GetClient()
        {
            var httpClient = new HttpClient();

            return new CaClient(new Uri(this.caUrl), httpClient, this.caAccountId, this.caPassword);
        }

        /// <inheritdoc/>
        public int CreateAccount(string name, string organizationUnit, string organization, string locality, string stateOrProvince, string emailAddress, string country, string[] requestedPermissions = null)
        {
            CaClient caClient = this.GetClient();
            return caClient.CreateAccount(name, organizationUnit, organization, locality, stateOrProvince, emailAddress, country, requestedPermissions);
        }

        public X509Certificate RequestNewCertificate(Key privateKey, PubKey transactionSigningPubKey, PubKey blockSigningPubKey)
        {
            CaClient caClient = this.GetClient();

            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel csrModel = caClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(transactionSigningPubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(csrModel.CertificateSigningRequestContent, privateKey, "secp256k1");

            CertificateInfoModel issuedCertificate = caClient.IssueCertificate(signedCsr);

            return issuedCertificate.ToCertificate();
        }

        public List<PubKey> GetCertificatePublicKeys()
        {
            CaClient caClient = this.GetClient();
            return caClient.GetCertificatePublicKeys(this.logger);
        }

        /// <summary>
        /// Determines whether a certificate has been revoked by checking the sender (node)'s address.
        /// </summary>
        /// <param name="address">The address of the node.</param>
        /// <returns><c>true</c> if the given certificate has been revoked.</returns>
        public bool IsCertificateRevokedByAddress(uint160 address)
        {
            return this.revocationChecker.IsCertificateRevokedByTransactionSigningKeyHash(address.ToBytes());
        }

        public static byte[] ExtractCertificateExtension(X509Certificate certificate, string oid)
        {
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                    // This is truly horrible, but it isn't clear how we can correctly go from the DER bytes in the extension, to a relevant BC class, to a string.
                    // Perhaps we are meant to recursively evaluate the extension data as ASN.1 until we land up with raw data that can't be decoded further?
                    // IMPORTANT: The two prefix bytes being removed consist of a type tag (e.g. `0x04` = octet string)
                    // and a length byte. For lengths > 127 more than one byte is needed, which would break this code.
                    return extension.RawData.Skip(2).ToArray();
            }

            return null;
        }

        public static string ExtractCertificateExtensionString(X509Certificate certificate, string oid)
        {
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                {
                    // This is truly horrible, but it isn't clear how we can correctly go from the DER bytes in the extension, to a relevant BC class, to a string.
                    // Perhaps we are meant to recursively evaluate the extension data as ASN.1 until we land up with raw data that can't be decoded further?
                    var temp = extension.RawData.Skip(2).ToArray();

                    return Encoding.UTF8.GetString(temp);
                }
            }

            return null;
        }
    }

    /// <summary>Exception that is thrown when certificates configuration is incorrect.</summary>
    public class CertificateConfigurationException : Exception
    {
        public CertificateConfigurationException(string message) : base(message)
        {
        }
    }
}
