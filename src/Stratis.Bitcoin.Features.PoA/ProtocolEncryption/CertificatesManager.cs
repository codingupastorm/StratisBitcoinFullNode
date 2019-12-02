using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;

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

        private readonly TextFileConfiguration configuration;

        public CertificatesManager(DataFolder dataFolder, NodeSettings nodeSettings, ILoggerFactory loggerFactory, RevocationChecker revocationChecker)
        {
            this.dataFolder = dataFolder;
            this.configuration = nodeSettings.ConfigReader;
            this.revocationChecker = revocationChecker;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>Loads client and authority certificates and validates them.</summary>
        /// <exception cref="CertificateConfigurationException">Thrown in case required certificates are not found or are not valid.</exception>
        public async Task InitializeAsync()
        {
            string acPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AuthorityCertificateName);
            string clientCertPath = Path.Combine(this.dataFolder.RootPath, ClientCertificateName);

            if (!File.Exists(acPath))
            {
                this.logger.LogTrace("(-)[AC_NOT_FOUND]");
                throw new CertificateConfigurationException($"Authority certificate wasn't found! Make sure you place '{AuthorityCertificateName}' in node's root directory.");
            }

            if (!File.Exists(clientCertPath))
            {
                this.logger.LogTrace("(-)[CC_NOT_FOUND]");
                throw new CertificateConfigurationException($"Client certificate wasn't found! Make sure you place '{ClientCertificateName}' in node's root directory.");
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

            bool revoked = await this.revocationChecker.IsCertificateRevokedAsync(this.ClientCertificate.Thumbprint).ConfigureAwait(false);

            if (revoked)
                throw new Exception("Provided client certificate was revoked!");
        }

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        private bool IsSignedByAuthorityCertificate(X509Certificate2 certificateToValidate, X509Certificate2 authorityCertificate)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);

            chain.ChainPolicy.ExtraStore.Add(authorityCertificate);

            bool isChainValid = chain.Build(certificateToValidate);

            if (!isChainValid)
            {
                string[] errors = chain.ChainStatus.Select(x => $"{x.StatusInformation.Trim()} ({x.Status})").ToArray();
                string certificateErrorsString = "Unknown errors.";

                if (errors.Length > 0)
                    certificateErrorsString = string.Join(", ", errors);

                throw new Exception("Trust chain did not complete to the known authority anchor. Errors: " + certificateErrorsString);
            }

            // This piece makes sure it actually matches your known root
            bool valid = chain.ChainElements.Cast<X509ChainElement>().Any(x => x.Certificate.Thumbprint == authorityCertificate.Thumbprint);

            return valid;
        }

        public bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain _, SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null)
                return false;

            var certificateToValidate = new X509Certificate2(certificate);

            X509Chain chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);

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

            bool revoked = this.revocationChecker.IsCertificateRevokedAsync(this.ClientCertificate.Thumbprint).ConfigureAwait(false).GetAwaiter().GetResult();

            return !revoked;
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
