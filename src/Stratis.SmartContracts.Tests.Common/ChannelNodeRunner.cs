using System.IO;
using NBitcoin;
using Stratis.Core;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Core.P2P;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.Api;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.SmartContracts;
using Stratis.SmartContracts.Tokenless;
using TextFileConfiguration = Stratis.Core.Configuration.TextFileConfiguration;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class ChannelNodeRunner : NodeRunner
    {
        private readonly string[] args;
        private readonly IDateTimeProvider dateTimeProvider;

        public ChannelNodeRunner(string[] args, string dataFolder, EditableTimeProvider timeProvider)
            : base(dataFolder, null)
        {
            this.args = args;
            this.dateTimeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            Network network = null;

            // TODO-TL: This needs to be moved someplace else.
            var configReader = new TextFileConfiguration(args);
            var configurationFile = configReader.GetOrDefault("conf", "");
            var dataDir = configReader.GetOrDefault("datadir", "");
            var configurationFilePath = Path.Combine(dataDir, configurationFile);
            var fileConfig = new TextFileConfiguration(File.ReadAllText(configurationFilePath));
            fileConfig.MergeInto(configReader);


            var channelSettings = new ChannelSettings(configReader);
            network = ChannelNetwork.Construct(dataDir, channelSettings.ChannelName);
            NodeSettings nodeSettings = new NodeSettings(network, agent: $"Channel-{channelSettings.ChannelName}", configReader: configReader);

            IFullNodeBuilder builder = new FullNodeBuilder()
                .UseNodeSettings(nodeSettings)
                .UseBlockStore()
                .UseTokenlessPoaConsenus(network)
                .UseMempool()
                .UseApi()
                .UseTokenlessKeyStore()
                .AddSmartContracts(options =>
                {
                    options.UseTokenlessReflectionExecutor();
                    options.UseSmartContractType<TokenlessSmartContract>();
                })
                .AsTokenlessNetwork()
                .ReplaceTimeProvider(this.dateTimeProvider)
                .MockIBD()
                .AddTokenlessFastMiningCapability();

            builder.RemoveImplementation<PeerConnectorDiscovery>();
            builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());

            this.FullNode = (FullNode)builder.Build();
        }
    }
}
