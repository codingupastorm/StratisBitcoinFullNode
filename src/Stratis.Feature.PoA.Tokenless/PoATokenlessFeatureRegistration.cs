using NBitcoin;
using Stratis.Bitcoin.Builder;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class PoATokenlessFeatureRegistration
    {
        /// <summary>This is mandatory for all PoA networks.</summary>
        public static IFullNodeBuilder UsePoAConsensus(this IFullNodeBuilder fullNodeBuilder, Network network)
        {
            //fullNodeBuilder.ConfigureFeature(features =>
            //{
            //    features
            //        .AddFeature<PoATokenlessFeature>()
            //        .DependOn<ConsensusFeature>()
            //        .FeatureServices(services =>
            //        {
            //            services.AddSingleton<IFederationManager, FederationManager>();
            //            services.AddSingleton<PoABlockHeaderValidator>();
            //            services.AddSingleton<IPoAMiner, PoAMiner>();
            //            services.AddSingleton<MinerSettings>();
            //            services.AddSingleton<PoAMinerSettings>();
            //            services.AddSingleton<ISlotsManager, SlotsManager>();
            //            services.AddSingleton<BlockDefinition, PoABlockDefinition>();
            //        });
            //});

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
