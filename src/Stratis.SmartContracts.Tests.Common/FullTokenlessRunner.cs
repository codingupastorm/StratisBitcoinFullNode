using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class FullTokenlessRunner : NodeRunner
    {
        private readonly IDateTimeProvider timeProvider;

        public FullTokenlessRunner(string dataDir, Network network, EditableTimeProvider timeProvider)
            : base(dataDir, null)
        {
            this.Network = network;
            this.timeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            IFullNodeBuilder builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseTokenlessPoaConsenus(this.Network)
                .UseMempool()
                .UseTokenlessWallet()
                .AddRPC()
                .AddSmartContracts(options =>
                {
                    options.UseTokenlessReflectionExecutor();
                    options.UseSmartContractType<TokenlessSmartContract>();
                })
                .AsTokenlessNetwork()
                .ReplaceTimeProvider(this.timeProvider)
                .MockIBD()
                .AddTokenlessFastMiningCapability();

            if (!this.EnablePeerDiscovery)
            {
                builder.RemoveImplementation<PeerConnectorDiscovery>();
                builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());
            }

            this.FullNode = (FullNode)builder.Build();
        }
    }
}
