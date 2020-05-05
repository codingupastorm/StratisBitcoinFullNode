using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Stratis.Feature.PoA.Tokenless.ProtocolEncryption
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

        private readonly ILogger logger;

        private readonly Network network;

        private readonly TextFileConfiguration configuration;

        private readonly string caUrl;

        private string caPassword;

        private int caAccountId;

        private string clientCertificatePassword;

        private readonly IMembershipServicesDirectory membershipServices;

        public CertificatesManager(DataFolder dataFolder, NodeSettings nodeSettings, ILoggerFactory loggerFactory, Network network, IMembershipServicesDirectory membershipServices)
        {
            this.dataFolder = dataFolder;
            this.configuration = nodeSettings.ConfigReader;
            this.network = network;
            this.membershipServices = membershipServices;

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

            bool clientCertValid = this.membershipServices.IsSignedByAuthorityCertificate(this.ClientCertificate, this.AuthorityCertificate);

            if (!clientCertValid)
                throw new CertificateConfigurationException("Provided client certificate isn't valid or isn't signed by the authority certificate!");

            bool revoked = this.membershipServices.IsCertificateRevoked(CaCertificatesManager.GetThumbprint(this.ClientCertificate));

            if (revoked)
                throw new CertificateConfigurationException("Provided client certificate was revoked!");

            return true;
        }

        public CaClient GetClient()
        {
            var httpClient = new HttpClient();

            return new CaClient(new Uri(this.caUrl), httpClient, this.caAccountId, this.caPassword);
        }
        
        public List<PubKey> GetCertificatePublicKeys()
        {
            CaClient caClient = this.GetClient();
            return caClient.GetCertificatePublicKeys(this.logger);
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
