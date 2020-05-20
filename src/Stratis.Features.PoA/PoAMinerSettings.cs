using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Core.Mining;
using Stratis.Core.Utilities;

namespace Stratis.Features.PoA
{
    public class PoAMinerSettings : IMinerSettings
    {
        /// <summary>Allows mining in case node is in IBD and not connected to anyone.</summary>
        public bool BootstrappingMode { get; private set; }

        /// <inheritdoc />
        public BlockDefinitionOptions BlockDefinitionOptions { get; private set; }

        public PoAMinerSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.BootstrappingMode = config.GetOrDefault<bool>("bootstrap", false);

            uint blockMaxSize = (uint)config.GetOrDefault<int>("blockmaxsize", (int)nodeSettings.Network.Consensus.Options.MaxBlockSerializedSize);
            uint blockMaxWeight = (uint)config.GetOrDefault<int>("blockmaxweight", (int)nodeSettings.Network.Consensus.Options.MaxBlockWeight);

            this.BlockDefinitionOptions = new BlockDefinitionOptions(blockMaxWeight, blockMaxSize).RestrictForNetwork(nodeSettings.Network);
        }

        public void DisableBootstrap()
        {
            this.BootstrappingMode = false;
        }

        /// <summary>
        /// Displays mining help information on the console.
        /// </summary>
        /// <param name="network">Not used.</param>
        public static void PrintHelp(Network network)
        {
            NodeSettings defaults = NodeSettings.Default(network);
            var builder = new StringBuilder();

            builder.AppendLine("-bootstrap                          Bootstraps the blockchain by allowing the node to perform solo mining.");
            builder.AppendLine("-blockmaxsize=<number>              Maximum block size (in bytes) for the miner to generate.");
            builder.AppendLine("-blockmaxweight=<number>            Maximum block weight (in weight units) for the miner to generate.");

            defaults.Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Miner Settings####");
            builder.AppendLine("#Bootstraps the blockchain by allowing the node to perform solo mining.");
            builder.AppendLine($"#bootstrap=0");
            builder.AppendLine("#Maximum block size (in bytes) for the miner to generate.");
            builder.AppendLine($"#blockmaxsize={network.Consensus.Options.MaxBlockSerializedSize}");
            builder.AppendLine("#Maximum block weight (in weight units) for the miner to generate.");
            builder.AppendLine($"#blockmaxweight={network.Consensus.Options.MaxBlockWeight}");
        }
    }
}
