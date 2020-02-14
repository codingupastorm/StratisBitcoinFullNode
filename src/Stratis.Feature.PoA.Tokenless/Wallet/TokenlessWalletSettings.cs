﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public class TokenlessWalletSettings
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public int AccountAddressIndex { get; set; }

        public int MiningAddressIndex { get; set; }

        public int CertificateAddressIndex { get; set; }

        public string EncryptedSeed { get; set; }

        public string Password { get; set; }

        public string Mnemonic { get; set; }

        public string RootPath { get; set; }

        // CA certificate related settings.

        public bool GenerateCertificate { get; set; }

        public string CertPath { get; set; }

        public string Name { get; set; }

        public string OrganizationUnit { get; set; }

        public string Organization { get; set; }

        public string Locality { get; set; }

        public string StateOrProvince { get; set; }

        public string EmailAddress { get; set; }

        public string Country { get; set; }

        private bool IsRelativePath(string path)
        {
            return !this.CertPath.Contains(":\\") && !this.CertPath.StartsWith("/");
        }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public TokenlessWalletSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(TokenlessWalletSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.AccountAddressIndex = config.GetOrDefault<int>("accountaddressindex", 0, this.logger);
            this.MiningAddressIndex = config.GetOrDefault<int>("miningaddressindex", 0, this.logger);
            this.CertificateAddressIndex = config.GetOrDefault<int>("certificateaddressindex", 0, this.logger);
            this.Password = config.GetOrDefault<string>("password", null, this.logger);
            this.Mnemonic = config.GetOrDefault<string>("mnemonic", null, this.logger);
            this.RootPath = nodeSettings.DataFolder.RootPath;

            this.GenerateCertificate = config.GetOrDefault<bool>("generatecertificate", false, this.logger);
            this.CertPath = Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.ClientCertificateName);
            
            this.Name = config.GetOrDefault<string>("certificatename", "", this.logger);
            this.OrganizationUnit = config.GetOrDefault<string>("certificateorganizationunit", "", this.logger);
            this.Organization = config.GetOrDefault<string>("certificateorganization", "", this.logger);
            this.Locality = config.GetOrDefault<string>("certificatelocality", "", this.logger);
            this.StateOrProvince = config.GetOrDefault<string>("certificatestateorprovince", "", this.logger);
            this.EmailAddress = config.GetOrDefault<string>("certificateemailaddress", null, this.logger);
            this.Country = config.GetOrDefault<string>("certificatecountry", "", this.logger);

            if (this.GenerateCertificate)
            {
                if (string.IsNullOrEmpty(this.Name))
                    throw new ConfigurationException("You need to specify a name for the node's certificate.");

                if (string.IsNullOrEmpty(this.OrganizationUnit))
                    throw new ConfigurationException("You need to specify an organization unit for the node's certificate.");

                if (string.IsNullOrEmpty(this.Organization))
                    throw new ConfigurationException("You need to specify an organization for the node's certificate.");

                if (string.IsNullOrEmpty(this.Locality))
                    throw new ConfigurationException("You need to specify a locality for the node's certificate.");

                if (string.IsNullOrEmpty(this.StateOrProvince))
                    throw new ConfigurationException("You need to specify a state or province for the node's certificate.");

                if (string.IsNullOrEmpty(this.EmailAddress))
                    throw new ConfigurationException("You need to specify an email address for the node's certificate.");

                //if (this.EmailAddress != null && !IsEmail(this.EmailAddress))
                //    throw new ConfigurationException($"The supplied e-mail address ('{ this.EmailAddress }') syntax is invalid.");

                if (string.IsNullOrEmpty(this.Country))
                    throw new ConfigurationException("You need to specify a country for the node's certificate.");
            }
        }

        /// <summary>
        /// Displays wallet configuration help information on the console.
        /// </summary>
        /// <param name="network">Not used.</param>
        public static void PrintHelp(Network network)
        {
            NodeSettings defaults = NodeSettings.Default(network);
            var builder = new StringBuilder();

            builder.AppendLine("-password=<string>              Provides a password when creating or using the wallet.");
            builder.AppendLine("-mnemonic=<string>              Provides a mnemonic when creating the wallet.");
            builder.AppendLine("-accountaddressindex=<number>   The index (N) used for the transaction signing key at HD Path (m/44'/105'/0'/0/N) where N is a zero based key ID.");
            builder.AppendLine("-miningaddressindex=<number>    The index (N) used for the block signing key at HD Path (m/44'/105'/1'/0/N) where N is a zero based key ID.");
            builder.AppendLine("-certaddressindex=<number>      The index (N) used for the P2P certificate key at HD Path (m/44'/105'/2'/0/N) where N is a zero based key ID.");
            builder.AppendLine("----Certificate Details----");
            builder.AppendLine("-generatecertificate                  Requests a new certificate to be generated.");
            builder.AppendLine("-certificatename=<string>             The user's name, as it should appear on the certificate request.");
            builder.AppendLine("-certificateorganizationunit=<string> The user's organization unit.");
            builder.AppendLine("-certificateorganization=<string>     The user's organization.");
            builder.AppendLine("-certificatelocality=<string>         The user's locality.");
            builder.AppendLine("-certificatestateorprovince=<string>  The user's state or province.");
            builder.AppendLine("-certificateemailaddress=<string>     The user's email address.");
            builder.AppendLine("-certificatecountry=<string>          The user's country.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Wallet Settings####");
            builder.AppendLine("#The account address index.");
            builder.AppendLine("#accountaddressindex=0");
            builder.AppendLine("#The mining address index.");
            builder.AppendLine("#miningaddressindex=0");
            builder.AppendLine("#The certificate address index.");
            builder.AppendLine("#certaddressindex=0");
            builder.AppendLine("#----Certificate Details----");
            builder.AppendLine("#Requests a new certificate to be generated.");
            builder.AppendLine("#generatecertificate=false");
            builder.AppendLine("#certificatename=");
            builder.AppendLine("#certificateorganizationunit=");
            builder.AppendLine("#certificateorganization=");
            builder.AppendLine("#certificatelocality=");
            builder.AppendLine("#certificatestateorprovince=");
            builder.AppendLine("#certificateemailaddress=");
            builder.AppendLine("#certificatecountry=");
        }

        /// <summary>
        /// Regular expression, which is used to validate an E-Mail address.
        /// See https://www.oreilly.com/library/view/regular-expressions-cookbook/9780596802837/ch04.html
        /// </summary>
        private const string matchEmailPattern =
            @"^[\w!#$%&'*+/=?`{|}~^-]+(?:\.[\w!#$%&'*+/=?`{|}~^-]+)*@(?:[A-Z0-9-]+\.)+[A-Z]{2,6}$";

        /// <summary>
        /// Checks whether the given e-mail address is a valid e-mail address.
        /// </summary>
        /// <param name="email">An e-Mail address.</param>
        /// <returns>True, when Parameter-string is not null and 
        /// contains a valid E-Mail address;
        /// otherwise false.</returns>
        private static bool IsEmail(string email)
        {
            return email != null && Regex.IsMatch(email, matchEmailPattern);
        }

        /// <summary>
        /// https://www.oreilly.com/library/view/regular-expressions-cookbook/9781449327453/ch04s03.html
        /// </summary>
        private const string matchPhonePattern = "^\\+(?:[0-9] ?){6,14}[0-9]$";

        /// <summary>
        /// Checks whether the given phone number parameter is a valid phone number.
        /// </summary>
        /// <param name="phone">A phone number.</param>
        /// <returns><c>True</c>, when phone number is not null and contains a valid phone number; otherwise false.</returns>
        private static bool IsPhone(string phone)
        {
            if (phone != null)
                return Regex.IsMatch(phone, matchPhonePattern);

            return false;
        }
    }
}