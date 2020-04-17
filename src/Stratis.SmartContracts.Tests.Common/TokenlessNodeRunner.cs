﻿using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Api;
using Stratis.Features.PoA.Tests.Common;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.Tests.Common
{
    public sealed class TokenlessNodeRunner : NodeRunner
    {
        private readonly IDateTimeProvider timeProvider;

        public TokenlessNodeRunner(string dataDir, Network network, EditableTimeProvider timeProvider)
            : base(dataDir, null)
        {
            this.Network = network;
            this.timeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] {
                "-conf=poa.conf",
                "-datadir=" + this.DataFolder,
                $"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId}",
                $"-{CertificatesManager.CaPasswordKey}={CaTestHelper.AdminPassword}",
                $"-{CertificatesManager.ClientCertificateConfigurationKey}=test"
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