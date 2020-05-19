using System.Linq;
using MembershipServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using NBitcoin.PoA;
using Stratis.Core.Mining;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Core.Configuration.Logging;
using Stratis.Core.Consensus;
using Stratis.Core.Interfaces;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mining;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.Consensus;
using Stratis.Features.Consensus.CoinViews;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Features.PoA;
using Stratis.Features.PoA.Voting;
using Stratis.Features.SmartContracts;
using Stratis.Features.SmartContracts.Interfaces;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Store;

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
                        // Core
                        services.AddSingleton<ICoreComponent, CoreComponent>();

                        // Channels
                        services.AddSingleton<IChannelService, ProcessChannelService>();
                        services.AddSingleton<IChannelCreationExecutor, ChannelCreationExecutor>();
                        services.AddSingleton<IChannelUpdateExecutor, ChannelUpdateExecutor>();
                        services.AddSingleton<ChannelSettings>();
                        services.AddSingleton<IChannelAccessValidator, ChannelAccessValidator>();

                        // Mempool
                        services.Replace(ServiceDescriptor.Singleton<ITxMempool, TokenlessMempool>());
                        services.Replace(ServiceDescriptor.Singleton<IMempoolValidator, TokenlessMempoolValidator>());

                        //Mining
                        services.AddSingleton<BlockDefinition, TokenlessBlockDefinition>();

                        services.AddSingleton<ITokenlessSigner, TokenlessSigner>();
                        services.AddSingleton<ITokenlessBroadcaster, TokenlessBroadcaster>();

                        // Endorsement. For now everyone gets this. May not be the case in the future.
                        services.AddSingleton<IEndorsementRequestHandler, EndorsementRequestHandler>();
                        services.AddSingleton<IEndorsementSuccessHandler, EndorsementSuccessHandler>();
                        services.AddSingleton<IEndorsementRequestValidator, EndorsementRequestValidator>();
                        services.AddSingleton<IEndorsementSigner, EndorsementSigner>();
                        services.AddSingleton<IEndorsements, Endorsements>();
                        services.AddSingleton<IEndorsedTransactionBuilder, EndorsedTransactionBuilder>();
                        services.AddSingleton<IOrganisationLookup, OrganisationLookup>();
                        services.AddSingleton<IEndorsementSignatureValidator, EndorsementSignatureValidator>();
                        services.AddSingleton<IEndorsementPolicyValidator, EndorsementPolicyValidator>();
                        services.AddSingleton<EndorsedContractTransactionValidationRule>(); // Shared logic implementation for mempool/consensus rule.
                        services.AddSingleton<IPrivateDataRetriever, PrivateDataRetriever>();
                        services.AddSingleton<ReadWriteSetPolicyValidator>();
                        services.AddSingleton<IReadWriteSetTransactionSerializer, ReadWriteSetTransactionSerializer>();
                        services.AddSingleton<IReadWriteSetValidator, ReadWriteSetValidator>();

                        // Private data.
                        services.AddSingleton<ITransientKeyValueStore, TransientKeyValueStore>();
                        services.AddSingleton<ITransientStore, TransientStore>();
                        services.AddSingleton<IPrivateDataKeyValueStore, PrivateDataKeyValueStore>();
                        services.AddSingleton<IPrivateDataStore, PrivateDataStore>();

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
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

                        // PoA Specific
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IPollsKeyValueStore, PollsKeyValueStore>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();

                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<IModifiedFederation, ModifiedFederation>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<IMinerSettings, PoAMinerSettings>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();

                        // Smart Contract Specific
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();

                        // Permissioned membership.
                        services.AddSingleton<IMembershipServicesDirectory, MembershipServicesDirectory>();
                        services.AddSingleton<ICertificatePermissionsChecker, CertificatePermissionsChecker>();

                        // Channels
                        services.AddSingleton<IChannelKeyValueStore, ChannelKeyValueStore>();
                        services.AddSingleton<IChannelRepository, ChannelRepository>();
                        services.AddSingleton<IChannelRequestSerializer, ChannelRequestSerializer>();

                        var options = (PoAConsensusOptions)network.Consensus.Options;
                        if (options.EnablePermissionedMembership)
                        {
                            ServiceDescriptor descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INetworkPeerFactory));
                            services.Remove(descriptor);
                            services.AddSingleton<IClientCertificateValidator, ClientCertificateValidator>();
                            services.AddSingleton<INetworkPeerFactory, TlsEnabledNetworkPeerFactory>();
                        }

                        // Necessary for the dynamic contract controller
                        // Use AddScoped for instance-per-request lifecycle, ref. https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2#scoped
                        services.AddScoped<TokenlessController>();
                    });
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseTokenlessKeyStore(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<TokenlessKeyStoreFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<TokenlessKeyStoreSettings>();
                        services.AddSingleton<ITokenlessKeyStoreManager, TokenlessKeyStoreManager>();
                        services.AddSingleton<IMiningKeyProvider, TokenlessMiningKeyProvider>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
