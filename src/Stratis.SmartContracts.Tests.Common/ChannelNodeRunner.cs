﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Api;
using Stratis.Features.PoA;
using Stratis.Features.PoA.IntegrationTests.Common;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
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
            var loadedJson = File.ReadAllText($"{this.DataFolder}\\channels\\{this.channelName}_network.json");

            ChannelNetwork channelNetwork = JsonSerializer.Deserialize<ChannelNetwork>(loadedJson);

            // TODO-TL: Find a better way to construct the following items when deserializing.
            channelNetwork.Consensus.ConsensusFactory = new TokenlessConsensusFactory();
            channelNetwork.Consensus.ConsensusRules = new NBitcoin.ConsensusRules();
            channelNetwork.Consensus.HashGenesisBlock = channelNetwork.Genesis.GetHash();
            channelNetwork.Consensus.Options = new PoAConsensusOptions(0, 0, 0, 0, 0, new List<IFederationMember>(), 16, false, false, false);
            channelNetwork.Consensus.MempoolRules = new List<Type>();

            var settings = new NodeSettings(channelNetwork, args: new string[]
            {
                "-certificatepassword=test",
                "-password=test",
                "-conf=poa.conf",
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
