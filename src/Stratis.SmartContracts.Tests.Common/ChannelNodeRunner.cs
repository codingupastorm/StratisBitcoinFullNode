using System.IO;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Stratis.Bitcoin;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.P2P;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.Api;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.SmartContracts;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class ChannelNodeRunner : NodeRunner
    {
        private readonly string channelName;
        private readonly IDateTimeProvider dateTimeProvider;

        public ChannelNodeRunner(string channelName, string dataFolder, EditableTimeProvider timeProvider)
            : base(dataFolder, null)
        {
            this.channelName = channelName;
            this.dateTimeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var channelNetwork = ChannelNetwork.Construct(Path.Combine(this.DataFolder, "channels"), this.channelName);

            var settings = new NodeSettings(channelNetwork, args: new string[]
            {
                "-certificatepassword=test",
                "-password=test",
                "-conf=channel.conf",
                "-datadir=" + this.DataFolder,
                $"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId}",
                $"-{CertificatesManager.CaPasswordKey}={CaTestHelper.AdminPassword}",
                $"-{CertificatesManager.ClientCertificateConfigurationKey}=test",
                "-ischannelnode=true",
            });

            IFullNodeBuilder builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseTokenlessPoaConsenus(channelNetwork)
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
