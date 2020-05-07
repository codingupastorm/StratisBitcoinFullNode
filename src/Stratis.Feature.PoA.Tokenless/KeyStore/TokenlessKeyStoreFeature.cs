using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Core.Utilities;

namespace Stratis.Feature.PoA.Tokenless.KeyStore
{
    /// <summary>
    /// Feature for HD Wallet functionality.
    /// </summary>
    /// <seealso cref="FullNodeFeature" />
    public class TokenlessKeyStoreFeature : FullNodeFeature
    {
        private readonly ITokenlessKeyStoreManager keyStoreManager;

        public TokenlessKeyStoreFeature(ITokenlessKeyStoreManager keyStoreManager, INodeStats nodeStats)
        {
            this.keyStoreManager = keyStoreManager;

            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, this.GetType().Name);
            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component, this.GetType().Name);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            TokenlessKeyStoreSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            TokenlessKeyStoreSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            this.keyStoreManager.Initialize();

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
