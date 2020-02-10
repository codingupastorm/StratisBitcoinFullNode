using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
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
    }
}
