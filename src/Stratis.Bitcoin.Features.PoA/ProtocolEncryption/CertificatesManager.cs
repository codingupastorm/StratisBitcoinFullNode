using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class CertificatesManager
    {
        /// <summary>Name of authority .crt certificate that is supposed to be found in application folder.</summary>
        /// <remarks>This certificate is automatically copied during the build.</remarks>
        public const string AuthorityCertificateName = "AuthorityCertificate.crt";

        /// <summary>Name of client's .pfx certificate that is supposed to be found in node's folder.</summary>
        public const string ClientCertificateName = "ClientCertificate.pfx";

        /// <summary>The password used to decrypt the PKCS#12 (.pfx) file containing the client certificate and private key.</summary>
        public const string ClientCertificateConfigurationKey = "certificatepassword";

        /// <summary>The account ID ('username') used by the node to query the CA.</summary>
        public const string CaAccountIdKey = "caaccountid";

        /// <summary>The password used by the node to query the CA.</summary>
        public const string CaPasswordKey = "capassword";

        /// <summary>Root certificate of the certificate authority for the current network.</summary>
        public X509Certificate AuthorityCertificate { get; private set; }

        /// <summary>Client certificate that is used to establish connections with other peers.</summary>
        public X509Certificate ClientCertificate { get; private set; }

        /// <summary>The private key associated with the loaded client certificate. Intended to be used for TLS communication only.</summary>
        public AsymmetricKeyParameter ClientCertificatePrivateKey { get; private set; }

        private readonly DataFolder dataFolder;

        private readonly RevocationChecker revocationChecker;

        private readonly ILogger logger;

        private readonly Network network;

        private readonly TextFileConfiguration configuration;

        private string caUrl;

        private string caPassword;

        private int caAccountId;

        private string clientCertificatePassword;

        public CertificatesManager(DataFolder dataFolder, NodeSettings nodeSettings, ILoggerFactory loggerFactory, RevocationChecker revocationChecker, Network network)
        {
            this.dataFolder = dataFolder;
            this.configuration = nodeSettings.ConfigReader;
            this.revocationChecker = revocationChecker;
            this.network = network;

            this.caUrl = this.configuration.GetOrDefault<string>("caurl", "https://localhost:5001");

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Loads client and authority certificates and validates them.</summary>
        /// <exception cref="CertificateConfigurationException">Thrown in case required certificates are not found or are not valid.</exception>
        public async Task InitializeAsync()
        {
            this.LoadAuthorityCertificate();
            this.LoadClientCertificate();
        }

        public bool LoadAuthorityCertificate()
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

            if (this.caAccountId == 0)
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
                throw new CertificateConfigurationException($"You have to provide a password for the client certificate! Use '{ClientCertificateConfigurationKey}' configuration key to provide a password.");

            (this.ClientCertificate, this.ClientCertificatePrivateKey) = CaCertificatesManager.LoadPfx(File.ReadAllBytes(clientCertPath), this.clientCertificatePassword);

            if (this.ClientCertificate == null)
            {
                this.logger.LogTrace("(-)[WRONG_PASSWORD]");
                throw new CertificateConfigurationException($"Client certificate wasn't loaded. Usually this happens when provided password is incorrect.");
            }

            bool clientCertValid = this.IsSignedByAuthorityCertificate(this.ClientCertificate, this.AuthorityCertificate);

            if (!clientCertValid)
                throw new CertificateConfigurationException("Provided client certificate isn't valid or isn't signed by the authority certificate!");

            X509Certificate2 tempClientCert = CaCertificatesManager.ConvertCertificate(this.ClientCertificate, new SecureRandom());

            bool revoked = this.revocationChecker.IsCertificateRevokedAsync(tempClientCert.Thumbprint, false).ConfigureAwait(false).GetAwaiter().GetResult();

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

        public X509Certificate RequestNewCertificate(Key privateKey, PubKey transactionSigningPubKey, PubKey blockSigningPubKey)
        {
            CaClient caClient = this.GetClient();

            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel csrModel = caClient.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(transactionSigningPubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(csrModel.CertificateSigningRequestContent, privateKey, "secp256k1");

            CertificateInfoModel issuedCertificate = caClient.IssueCertificate(signedCsr);

            var certParser = new X509CertificateParser();
            X509Certificate certificate = certParser.ReadCertificate(issuedCertificate.CertificateContentDer);

            return certificate;
        }

        public X509Certificate GetCertificateForAddress(string address)
        {
            CaClient caClient = this.GetClient();

            CertificateInfoModel retrievedCertModel = caClient.GetCertificateForAddress(address);

            var certParser = new X509CertificateParser();
            X509Certificate certificate = certParser.ReadCertificate(retrievedCertModel.CertificateContentDer);

            return certificate;
        }

        public X509Certificate GetCertificateForPubKey(string pubKeyHash)
        {
            CaClient caClient = this.GetClient();

            CertificateInfoModel retrievedCertModel = caClient.GetCertificateForPubKeyHash(pubKeyHash);

            var certParser = new X509CertificateParser();
            X509Certificate certificate = certParser.ReadCertificate(retrievedCertModel.CertificateContentDer);

            return certificate;
        }

        public Task<List<PubKey>> GetCertificatePublicKeysAsync()
        {
            CaClient caClient = this.GetClient();

            return caClient.GetCertificatePublicKeysAsync();
        }

        public static byte[] ExtractCertificateExtension(X509Certificate certificate, string oid)
        {
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());
            
            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                    return extension.RawData;
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

        private static Asn1Object ToAsn1Object(byte[] data)
        {
            var inStream = new MemoryStream(data);
            var asnInputStream = new Asn1InputStream(inStream);

            return asnInputStream.ReadObject();
        }
    }

    /// <summary>Exception that is thrown when certificates configuration is incorrect.</summary>
    public class CertificateConfigurationException : Exception
    {
        public CertificateConfigurationException()
        {
        }

        public CertificateConfigurationException(string message) : base(message)
        {
        }
    }
}
