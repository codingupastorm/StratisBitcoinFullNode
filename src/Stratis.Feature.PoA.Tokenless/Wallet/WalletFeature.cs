using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    /// <summary>
    /// Feature for HD Wallet functionality.
    /// </summary>
    /// <seealso cref="Stratis.Bitcoin.Builder.Feature.FullNodeFeature" />
    public class WalletFeature : FullNodeFeature
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>The wallet manager.</summary>
        private readonly IDLTWalletManager walletManager;

        /// <summary>The wallet settings.</summary>
        private readonly WalletSettings walletSettings;

        public WalletFeature(
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IDLTWalletManager walletManager,
            WalletSettings walletSettings,
            INodeStats nodeStats)
        {
            this.nodeLifetime = nodeLifetime;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.walletSettings = walletSettings;
            this.walletManager = walletManager;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, this.GetType().Name);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            WalletSettings.PrintHelp(network);
        }
        
        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            WalletSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }

        public void AddInlineStats(StringBuilder log)
        {
        }

        public void AddComponentStats(StringBuilder log)
        {
        }
    }
}
