using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Features.Consensus;
using Stratis.Features.Consensus.CoinViews;
using Stratis.Features.PoA;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.Features.PoA.Voting;
using Stratis.Features.SmartContracts.Interfaces;
using Stratis.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;

namespace Stratis.Features.SmartContracts.PoA
{
    public static partial class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Configures the node with the smart contract proof of authority consensus model.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractPoAConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .DependOn<SmartContractFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IDBCoinViewStore, DBCoinViewStore>();
                        services.AddSingleton<DBCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();

                        services.AddSingleton(typeof(IContractTransactionPartialValidationRule), typeof(SmartContractFormatLogic));
                        services.AddSingleton<IConsensusRuleEngine, PoAConsensusRuleEngine>();

                        // Voting.
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IPollsKeyValueStore, PollsKeyValueStore>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();
                        services.AddSingleton<IdleFederationMembersKicker>();

                        // Purely to make DI work, shouldn't be used.
                        services.AddSingleton<ICertificatesManager, CertificatesManager>();
                        services.AddSingleton<IRevocationChecker, RevocationChecker>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
