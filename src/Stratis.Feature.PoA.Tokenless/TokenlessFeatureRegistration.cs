﻿using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mining;
using Stratis.Feature.PoA.Tokenless.Wallet;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class TokenlessFeatureRegistration
    {
        public static IFullNodeBuilder AsTokenlessNetwork(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<TokenlessFeature>()
                    .FeatureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<ITxMempool, TokenlessMempool>());
                        services.Replace(ServiceDescriptor.Singleton<IMempoolValidator, TokenlessMempoolValidator>());
                        services.AddSingleton<BlockDefinition, TokenlessBlockDefinition>();
                        services.AddSingleton<ITokenlessSigner, TokenlessSigner>();
                        services.AddSingleton<ICoreComponent, CoreComponent>();

                        // In place of wallet.
                        services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    });
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseTokenlessPoaConsenus(this IFullNodeBuilder fullNodeBuilder, Network network)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        // Base
                        services.AddSingleton<DBCoinView>();
                        services.AddSingleton<IDBCoinViewStore, DBCoinViewStore>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<IConsensusRuleEngine, TokenlessConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

                        // PoA Specific
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();

                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, TokenlessPoAMiner>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<PoAMinerSettings>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();

                        // Smart Contract Specific
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();

                        // Permissioned membership.
                        services.AddSingleton<CertificatesManager>();
                        services.AddSingleton<RevocationChecker>();

                        var options = (PoAConsensusOptions)network.Consensus.Options;
                        if (options.EnablePermissionedMembership)
                        {
                            ServiceDescriptor descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INetworkPeerFactory));
                            services.Remove(descriptor);
                            services.AddSingleton<INetworkPeerFactory, TlsEnabledNetworkPeerFactory>();
                        }
                    });
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseTokenlessWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<TokenlessWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<TokenlessWalletSettings>();
                        services.AddSingleton<ITokenlessWalletManager, TokenlessWalletManager>();
                        services.AddSingleton<IMiningKeyProvider, TokenlessMiningKeyProvider>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
