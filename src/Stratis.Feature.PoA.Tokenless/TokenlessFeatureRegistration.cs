using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mining;

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

                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<PoAMinerSettings>();
                    });
            });

            //LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            //fullNodeBuilder.ConfigureFeature(features =>
            //{
            //    features
            //        .AddFeature<ConsensusFeature>()
            //        .FeatureServices(services =>
            //        {
            //            services.AddSingleton<DBreezeCoinView>();
            //            services.AddSingleton<ICoinView, CachedCoinView>();
            //            services.AddSingleton<IConsensusRuleEngine, PoAConsensusRuleEngine>();
            //            services.AddSingleton<IChainState, ChainState>();
            //            services.AddSingleton<ConsensusQuery>()
            //                .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
            //                .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

            //            // Voting.
            //            services.AddSingleton<VotingManager>();
            //            services.AddSingleton<IPollResultExecutor, PollResultExecutor>();
            //            services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();
            //            services.AddSingleton<IdleFederationMembersKicker>();

            //            // Permissioned membership.
            //            services.AddSingleton<CertificatesManager>();
            //            services.AddSingleton<RevocationChecker>();

            //            var options = (PoAConsensusOptions)network.Consensus.Options;

            //            if (options.EnablePermissionedMembership)
            //            {
            //                ServiceDescriptor descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INetworkPeerFactory));
            //                services.Remove(descriptor);
            //                services.AddSingleton<INetworkPeerFactory, TlsEnabledNetworkPeerFactory>();
            //            }
            //        });
            //});

            return fullNodeBuilder;
        }
    }
}
