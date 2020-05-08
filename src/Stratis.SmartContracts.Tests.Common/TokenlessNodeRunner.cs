using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using NBitcoin;
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
using Stratis.Features.Api;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.SmartContracts;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class TokenlessNodeRunner : NodeRunner
    {
        private readonly IDateTimeProvider timeProvider;

        public TokenlessNodeRunner(string dataDir, Network network, EditableTimeProvider timeProvider, string agent)
            : base(dataDir, agent)
        {
            this.Network = network;
            this.timeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, agent: this.Agent, args: new string[] {
                "-conf=poa.conf",
                "-datadir=" + this.DataFolder,
                $"-{CertificateAuthorityInterface.CaAccountIdKey}={Settings.AdminAccountId}",
                $"-{CertificateAuthorityInterface.CaPasswordKey}={CaTestHelper.AdminPassword}",
                $"-{CertificateAuthorityInterface.ClientCertificateConfigurationKey}=test"
            });

            IFullNodeBuilder builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseTokenlessPoaConsenus(this.Network)
                .UseMempool()
                .UseApi()
                .UseTokenlessKeyStore()
                .AddSmartContracts(options =>
                {
                    options.UseTokenlessReflectionExecutor();
                    options.UseSmartContractType<TokenlessSmartContract>();
                })
                .AsTokenlessNetwork()
                .ReplaceTimeProvider(this.timeProvider)
                .MockIBD()
                .AddTokenlessFastMiningCapability();

            builder.RemoveImplementation<PeerConnectorDiscovery>();
            builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());

            this.FullNode = (FullNode)builder.Build();
        }
    }
}
