﻿using System.Text;
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

        public int AddressIndex { get; set; }

        public string EncryptedSeed { get; set; }

        public string Password { get; set; }

        public string Mnemonic { get; set; }

        public string RootPath { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public TokenlessWalletSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(TokenlessWalletSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.AddressIndex = config.GetOrDefault<int>("addressindex", 0, this.logger);
            this.Password = config.GetOrDefault<string>("password", null, this.logger);
            this.Mnemonic = config.GetOrDefault<string>("mnemonic", null, this.logger);
            this.RootPath = nodeSettings.DataFolder.RootPath;
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
            builder.AppendLine("-addressindex=<number>          The index (N) used for the transaction signing key at HD Path (m/44'/105'/0'/0/N) where N is a zero based key ID.");
            builder.AppendLine("                                The index (N) used for the block signing key at HD Path (m/44'/105'/1'/0/N) where N is a zero based key ID.");
            builder.AppendLine("                                The index (N) used for the P2P certificate key at HD Path (m/44'/105'/2'/0/N) where N is a zero based key ID.");

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
            builder.AppendLine("#The address index.");
            builder.AppendLine("#addressindex=0");
        }
    }
}