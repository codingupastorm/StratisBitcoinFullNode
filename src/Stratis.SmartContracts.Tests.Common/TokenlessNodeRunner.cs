using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using Stratis.Core;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Core.P2P;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Core.Builder.Feature;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Channels;
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
        private readonly SmartContractNodeBuilder nodeBuilder;

        public TokenlessNodeRunner(string dataDir, Network network, EditableTimeProvider timeProvider, string agent, SmartContractNodeBuilder nodeBuilder = null)
            : base(dataDir, agent)
        {
            this.Network = network;
            this.timeProvider = timeProvider;
            this.nodeBuilder = nodeBuilder;
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

            if (this.nodeBuilder != null)
            {
                builder.ConfigureFeature(features =>
                {
                    foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                    {
                        feature.FeatureServices(services =>
                        {
                            services.AddSingleton<SmartContractNodeBuilder>(this.nodeBuilder);
                            services.Replace(ServiceDescriptor.Singleton<IChannelService, InProcessChannelService>());
                        });
                    }
                });
            }

            this.FullNode = (FullNode)builder.Build();
        }
    }
}
