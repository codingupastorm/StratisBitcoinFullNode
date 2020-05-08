using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Builder;
using Stratis.Core.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Mining;
using Stratis.Core.Utilities;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.Miner.Interfaces;

[assembly: InternalsVisibleTo("Stratis.Features.Miner.Tests")]

namespace Stratis.Features.Miner
{
    /// <summary>
    /// Provides an ability to mine or stake.
    /// </summary>
    public class MiningFeature : FullNodeFeature
    {
        private readonly ConnectionManagerSettings connectionManagerSettings;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Settings relevant to mining or staking.</summary>
        private readonly MinerSettings minerSettings;

        /// <summary>POW miner.</summary>
        private readonly IPowMining powMining;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public MiningFeature(
            ConnectionManagerSettings connectionManagerSettings,
            Network network,
            IMinerSettings minerSettings,
            ILoggerFactory loggerFactory,
            IPowMining powMining)
        {
            this.connectionManagerSettings = connectionManagerSettings;
            this.network = network;

            Guard.Assert(minerSettings is MinerSettings);
            this.minerSettings = (MinerSettings)minerSettings;

            this.powMining = powMining;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            MinerSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            MinerSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <summary>
        /// Stop a Proof of Work miner.
        /// </summary>
        public void StopMining()
        {
            this.powMining?.StopMining();
            this.logger.LogInformation("Mining stopped.");
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            if ((this.minerSettings.Mine || this.minerSettings.Stake) && this.connectionManagerSettings.IsGateway)
                throw new ConfigurationException("The node cannot be configured as a gateway and mine or stake at the same time.");

            if (this.minerSettings.Mine)
            {
                string mineToAddress = this.minerSettings.MineAddress;
                // if (string.IsNullOrEmpty(mineToAddress)) ;
                //    TODO: get an address from the wallet.

                if (!string.IsNullOrEmpty(mineToAddress))
                {
                    this.logger.LogInformation("Mining enabled.");

                    this.powMining.Mine(BitcoinAddress.Create(mineToAddress, this.network).ScriptPubKey);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.StopMining();
        }

        /// <inheritdoc />
        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            // Mining and staking require block store feature.
            if (this.minerSettings.Mine || this.minerSettings.Stake)
            {
                services.Features.EnsureFeature<BlockStoreFeature>();
                var storeSettings = services.ServiceProvider.GetService<StoreSettings>();
                if (storeSettings.PruningEnabled)
                    throw new ConfigurationException("BlockStore prune mode is incompatible with mining and staking.");
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMiningExtension
    {
        /// <summary>
        /// Adds a mining feature to the node being initialized.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<IMinerSettings, MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds POW and POS miner components to the node, so that it can mine or stake.
        /// </summary>
        /// <param name="fullNodeBuilder">The object used to build the current node.</param>
        /// <returns>The full node builder, enriched with the new component.</returns>
        public static IFullNodeBuilder AddPowPosMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .DependOn<MempoolFeature>()
                    // TODO: Need a better way to check dependencies. This is really just dependent on IWalletManager...
                    // Alternatively "DependsOn" should take a list of features that will satisfy the dependency.
                    //.DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosPowBlockDefinition>();
                        services.AddSingleton<IMinerSettings, MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}