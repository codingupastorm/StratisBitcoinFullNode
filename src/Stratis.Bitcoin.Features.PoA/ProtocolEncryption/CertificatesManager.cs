using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Client;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class CertificatesManager
    {
        /// <summary>Name of authority .crt certificate that is supposed to be found in application folder.</summary>
        /// <remarks>This certificate is automatically copied during the build.</remarks>
        public const string AuthorityCertificateName = "AuthorityCertificate.crt";

        /// <summary>Name of client's .pfx certificate that is supposed to be found in node's folder.</summary>
        public const string ClientCertificateName = "ClientCertificate.pfx";

        public const string ClientCertificateConfigurationKey = "certificatepassword";

        /// <summary>Root certificate of the certificate authority for the current network.</summary>
        public X509Certificate2 AuthorityCertificate { get; private set; }

        /// <summary>Client certificate that is used to establish connections with other peers.</summary>
        public X509Certificate2 ClientCertificate { get; private set; }

        private readonly DataFolder dataFolder;

        private readonly RevocationChecker revocationChecker;

        private readonly ILogger logger;

        private readonly Network network;

        private readonly TextFileConfiguration configuration;

        private string caUrl;

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
            string acPath = Path.Combine(this.dataFolder.RootPath, AuthorityCertificateName);
            string clientCertPath = Path.Combine(this.dataFolder.RootPath, ClientCertificateName);

            if (!File.Exists(acPath))
            {
                this.logger.LogTrace("(-)[AC_NOT_FOUND]:{0}='{1}'", nameof(acPath), acPath);
                throw new CertificateConfigurationException($"Authority certificate not located at '{acPath}'. Make sure you place '{AuthorityCertificateName}' in the node's root directory.");
            }

            if (!File.Exists(clientCertPath))
            {
                this.logger.LogTrace("(-)[CC_NOT_FOUND]:{0}='{1}'", nameof(clientCertPath), clientCertPath);
                throw new CertificateConfigurationException($"Client certificate not located at '{clientCertPath}'. Make sure you place '{ClientCertificateName}' in the node's root directory.");
            }

            string clientCertificatePassword = this.configuration.GetOrDefault<string>(ClientCertificateConfigurationKey, null);

            if (clientCertificatePassword == null)
            {
                this.logger.LogTrace("(-)[NO_PASSWORD]");
                throw new CertificateConfigurationException($"You have to provide password for the client certificate! Use '{ClientCertificateConfigurationKey}' configuration key to provide a password.");
            }

            this.AuthorityCertificate = new X509Certificate2(acPath);
            this.ClientCertificate = new X509Certificate2(clientCertPath, clientCertificatePassword);

            if (this.ClientCertificate == null)
            {
                this.logger.LogTrace("(-)[WRONG_PASSWORD]");
                throw new CertificateConfigurationException($"Client certificate wasn't loaded. Usually this happens when provided password is incorrect.");
            }

            bool clientCertValid = this.IsSignedByAuthorityCertificate(this.ClientCertificate, this.AuthorityCertificate);

            if (!clientCertValid)
                throw new Exception("Provided client certificate isn't signed by the authority certificate!");

            bool revoked = await this.revocationChecker.IsCertificateRevokedAsync(this.ClientCertificate.Thumbprint, false).ConfigureAwait(false);

            if (revoked)
                throw new Exception("Provided client certificate was revoked!");
        }

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        private bool IsSignedByAuthorityCertificate(X509Certificate2 certificateToValidate, X509Certificate2 authorityCertificate)
        {
            var chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.NoCheck,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority,
                    VerificationTime = DateTime.Now,
                    UrlRetrievalTimeout = new TimeSpan(0, 0, 0)
                }
            };

            chain.ChainPolicy.ExtraStore.Add(authorityCertificate);

            // Without actually importing the certificate into the local machine's trusted root store this will not return true.
            chain.Build(certificateToValidate);

            // Therefore, we inspect the ChainStatus directly to see if the certificate chain was nominally valid
            bool isChainValid = false;

            foreach (X509ChainStatus chainStatus in chain.ChainStatus)
            {
                // There are other validation errors getting raised, we should ensure that only 'known' errors are allowed
                if (chainStatus.Status != X509ChainStatusFlags.UntrustedRoot &&
                    chainStatus.Status != X509ChainStatusFlags.HasNotSupportedCriticalExtension &&
                    chainStatus.Status != X509ChainStatusFlags.InvalidExtension)
                    return false;

                if (chainStatus.Status == X509ChainStatusFlags.UntrustedRoot)
                    isChainValid = true;
            }

            // This piece makes sure it actually matches your known root
            bool rootValid = chain.ChainElements.Cast<X509ChainElement>().Any(x => x.Certificate.Thumbprint == authorityCertificate.Thumbprint);

            return isChainValid && rootValid;
        }

        public bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain _, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null)
                return false;

            var certificateToValidate = new X509Certificate2(certificate);

            X509Chain chain = new X509Chain
            {
                ChainPolicy =
                {
                    RevocationMode = X509RevocationMode.NoCheck,
                    RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority,
                    VerificationTime = DateTime.Now,
                    UrlRetrievalTimeout = new TimeSpan(0, 0, 0)
                }
            };

            // Add root certificate.
            chain.ChainPolicy.ExtraStore.Add(this.AuthorityCertificate);

            bool isChainValid = chain.Build(certificateToValidate);

            if (!isChainValid)
            {
                string[] errors = chain.ChainStatus.Select(x => String.Format("{0} ({1})", x.StatusInformation.Trim(), x.Status)).ToArray();
                string certificateErrorsString = "Unknown errors.";

                if (errors.Length > 0)
                    certificateErrorsString = String.Join(", ", errors);

                throw new Exception("Trust chain did not complete to the known authority anchor. Errors: " + certificateErrorsString);
            }

            // This piece makes sure it actually matches your known root
            bool valid = chain.ChainElements.Cast<X509ChainElement>().Any(x => x.Certificate.Thumbprint == this.AuthorityCertificate.Thumbprint);

            if (!valid)
                throw new Exception("Trust chain did not complete to the known authority anchor. Thumbprints did not match.");

            bool revoked = this.revocationChecker.IsCertificateRevokedAsync(this.ClientCertificate.Thumbprint, false).ConfigureAwait(false).GetAwaiter().GetResult();

            return !revoked;
        }

        public X509Certificate RequestNewCertificate(Client caClient, int accountId, string password, Key privateKey)
        {
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            var generateCsrModel = new GenerateCertificateSigningRequestModel()
            {
                AccountId = accountId, Address = address.ToString(), Password = password, PubKey = Convert.ToBase64String(pubKey.ToBytes())
            };

            CertificateSigningRequestModel csrModel = caClient.Generate_certificate_signing_requestAsync(generateCsrModel).ConfigureAwait(false).GetAwaiter().GetResult();
            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(csrModel.CertificateSigningRequestContent, privateKey, "secp256k1");

            var issueCertModel = new IssueCertificateFromFileContentsModel()
            {
                AccountId = accountId, CertificateRequestFileContents = signedCsr, Password = password
            };

            CertificateInfoModel issuedCertificate = caClient.Issue_certificate_using_request_stringAsync(issueCertModel).GetAwaiter().GetResult();
            
            var certificate = new X509Certificate(Convert.FromBase64String(issuedCertificate.CertificateContentDer));

            return certificate;
        }

        public X509Certificate GetCertificateForAddress(Client caClient, int accountId, string password, string address)
        {
            var model = new CredentialsModelWithAddressModel()
            {
                AccountId = accountId,
                Address = address,
                Password = password
            };

            CertificateInfoModel retrievedCertModel = caClient.Get_certificate_for_addressAsync(model).GetAwaiter().GetResult();

            var certificate = new X509Certificate(Convert.FromBase64String(retrievedCertModel.CertificateContentDer));

            return certificate;
        }

        public static byte[] ExtractCertificateExtension(X509Certificate certificate, string oid)
        {
            var cert = new X509Certificate2(certificate);

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                    return extension.RawData;
            }

            return null;
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
