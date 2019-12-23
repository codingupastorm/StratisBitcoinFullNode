using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
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

        public Dictionary<string, string> CertificateAttributes { get; set; }

        public string UserFullName { get; set; }

        public string UserEMail { get; set; }

        public string UserTelephone { get; set; }

        public string UserFacsimile { get; set; }

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
            this.CertPath = config.GetOrDefault<string>("certpath", "cert.crt", this.logger);
            if (this.IsRelativePath(this.CertPath))
                this.CertPath = Path.Combine(nodeSettings.DataFolder.RootPath, this.CertPath);

            IEnumerable<string> certInfo = config.GetOrDefault<string>("certinfo", string.Empty, this.logger).Replace("\\,", "\0").Split(',').Select(t => t.Replace("\0", ",").Trim());
            this.CertificateAttributes = new Dictionary<string, string>();
            foreach ((string key, string value) in certInfo.Where(t => !string.IsNullOrEmpty(t)).Select(t => t.Split(':')).Select(a => (a[0].Trim(), string.Join(":", a.Skip(1)).Trim())))
                this.CertificateAttributes[key] = value;
            this.UserFullName = config.GetOrDefault<string>("userfullname", null, this.logger);
            this.UserEMail = config.GetOrDefault<string>("useremail", null, this.logger);
            this.UserTelephone = config.GetOrDefault<string>("userphone", null, this.logger);
            this.UserFacsimile = config.GetOrDefault<string>("userfax", null, this.logger);
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
            builder.AppendLine("-generatecertificate            Requests a new certificate to be generated.");
            builder.AppendLine("-certpath=<string>              Path to certificate.");
            builder.AppendLine("-certinfo=<string>              Certificate attributes - e.g. 'CN:Sample Cert, OU:R&D, O:Company Ltd., L:Dublin 4, S:Dublin, C:IE'.");
            builder.AppendLine("-userfullname=<string>          The full name of the user.");
            builder.AppendLine("-useremail=<string>             The e-mail address of the user.");
            builder.AppendLine("-userphone=<phone number>       The phone number of the user.");
            builder.AppendLine("-userfax=<fax number>           The fax number of the user.");

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
            builder.AppendLine("#Path to certificate. Defaults to 'cert.crt'.");
            builder.AppendLine("#certpath=cert.crt");
            builder.AppendLine("----Certificate Details----");
            builder.AppendLine("#Requests a new certificate to be generated.");
            builder.AppendLine("#generatecertificate=false");
            builder.AppendLine("#Certificate attributes - e.g. 'CN:Sample Cert, OU:R&D, O:Company Ltd., L:Dublin 4, S:Dublin, C:IE'.");
            builder.AppendLine("#certinfo=");
            builder.AppendLine("#The full name of the user.");
            builder.AppendLine("#userfullname=");
            builder.AppendLine("#The e-mail address of the user.");
            builder.AppendLine("#useremail=");
            builder.AppendLine("#The phone number of the user.");
            builder.AppendLine("#userphone=");
            builder.AppendLine("#The fax number of the user.");
            builder.AppendLine("#userfax=");
        }
    }
}