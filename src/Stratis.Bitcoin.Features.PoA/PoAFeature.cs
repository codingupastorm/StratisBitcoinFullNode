﻿using System.Linq;
using System.Threading.Tasks;
using MembershipServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA.Behaviors;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAFeature : FullNodeFeature
    {
        /// <summary>Manager of node's network connections.</summary>
        private readonly IConnectionManager connectionManager;

        /// <summary>Thread safe chain of block headers from genesis.</summary>
        private readonly ChainIndexer chainIndexer;

        private readonly IFederationManager federationManager;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        private readonly IConsensusManager consensusManager;

        /// <summary>A handler that can manage the lifetime of network peers.</summary>
        private readonly IPeerBanning peerBanning;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        private readonly VotingManager votingManager;

        private readonly Network network;

        private readonly IWhitelistedHashesRepository whitelistedHashesRepository;

        private readonly IdleFederationMembersKicker idleFederationMembersKicker;

        private readonly IChainState chainState;

        private readonly IBlockStoreQueue blockStoreQueue;

        private readonly ICertificatesManager certificatesManager;

        private readonly IRevocationChecker revocationChecker;

        public PoAFeature(IFederationManager federationManager, PayloadProvider payloadProvider, IConnectionManager connectionManager, ChainIndexer chainIndexer,
            IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory,
            VotingManager votingManager, Network network, IWhitelistedHashesRepository whitelistedHashesRepository,
            IdleFederationMembersKicker idleFederationMembersKicker, IChainState chainState, IBlockStoreQueue blockStoreQueue, ICertificatesManager certificatesManager,
            IRevocationChecker revocationChecker)
        {
            this.federationManager = federationManager;
            this.connectionManager = connectionManager;
            this.chainIndexer = chainIndexer;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.votingManager = votingManager;
            this.whitelistedHashesRepository = whitelistedHashesRepository;
            this.network = network;
            this.idleFederationMembersKicker = idleFederationMembersKicker;
            this.chainState = chainState;
            this.blockStoreQueue = blockStoreQueue;
            this.certificatesManager = certificatesManager;
            this.revocationChecker = revocationChecker;

            payloadProvider.DiscoverPayloads(this.GetType().Assembly);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;

            this.ReplaceConsensusManagerBehavior(connectionParameters);

            this.ReplaceBlockStoreBehavior(connectionParameters);

            this.federationManager.Initialize();
            this.whitelistedHashesRepository.Initialize();

            var options = (PoAConsensusOptions)this.network.Consensus.Options;

            if (options.VotingEnabled)
            {
                this.votingManager.Initialize();

                if (options.AutoKickIdleMembers)
                    this.idleFederationMembersKicker.Initialize();
            }

            if (options.EnablePermissionedMembership)
            {
                this.revocationChecker.Initialize();
                this.certificatesManager.Initialize();
            }
        }

        /// <summary>Replaces default <see cref="ConsensusManagerBehavior"/> with <see cref="PoAConsensusManagerBehavior"/>.</summary>
        private void ReplaceConsensusManagerBehavior(NetworkPeerConnectionParameters connectionParameters)
        {
            INetworkPeerBehavior defaultConsensusManagerBehavior = connectionParameters.TemplateBehaviors.FirstOrDefault(behavior => behavior is ConsensusManagerBehavior);

            if (defaultConsensusManagerBehavior == null)
            {
                throw new MissingServiceException(typeof(ConsensusManagerBehavior), "Missing expected ConsensusManagerBehavior.");
            }

            connectionParameters.TemplateBehaviors.Remove(defaultConsensusManagerBehavior);
            connectionParameters.TemplateBehaviors.Add(new PoAConsensusManagerBehavior(this.chainIndexer, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory));
        }

        /// <summary>Replaces default <see cref="PoABlockStoreBehavior"/> with <see cref="PoABlockStoreBehavior"/>.</summary>
        private void ReplaceBlockStoreBehavior(NetworkPeerConnectionParameters connectionParameters)
        {
            INetworkPeerBehavior defaultBlockStoreBehavior = connectionParameters.TemplateBehaviors.FirstOrDefault(behavior => behavior is BlockStoreBehavior);

            if (defaultBlockStoreBehavior == null)
            {
                throw new MissingServiceException(typeof(BlockStoreBehavior), "Missing expected BlockStoreBehavior.");
            }

            connectionParameters.TemplateBehaviors.Remove(defaultBlockStoreBehavior);
            connectionParameters.TemplateBehaviors.Add(new PoABlockStoreBehavior(this.chainIndexer, this.chainState, this.loggerFactory, this.consensusManager, this.blockStoreQueue));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.votingManager.Dispose();

            this.idleFederationMembersKicker.Dispose();

            if (((PoAConsensusOptions)this.network.Consensus.Options).EnablePermissionedMembership)
            {
                this.revocationChecker.Dispose();
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderConsensusExtension
    {
        /// <summary>This is mandatory for all PoA networks.</summary>
        public static IFullNodeBuilder UsePoAConsensus(this IFullNodeBuilder fullNodeBuilder, Network network)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PoAFeature>()
                    .DependOn<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<IMinerSettings, PoAMinerSettings>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();
                        services.AddSingleton<BlockDefinition, PoABlockDefinition>();
                    });
            });

            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IDBCoinViewStore, DBCoinViewStore>();
                        services.AddSingleton<DBCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<IConsensusRuleEngine, PoAConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

                        // Voting.
                        services.AddSingleton<VotingManager>();
                        services.AddSingleton<IPollsKeyValueStore, PollsKeyValueStore>();
                        services.AddSingleton<IPollResultExecutor, PollResultExecutor>();
                        services.AddSingleton<IWhitelistedHashesRepository, WhitelistedHashesRepository>();
                        services.AddSingleton<IdleFederationMembersKicker>();

                        // Permissioned membership.
                        // TODO: PoA shouldn't actually require the MSD. Perhaps remove TLS support from PoA entirely?
                        services.AddSingleton<IMembershipServicesDirectory, MembershipServicesDirectory>();
                        services.AddSingleton<ICertificatesManager, CertificatesManager>();
                        services.AddSingleton<IRevocationChecker, RevocationChecker>();

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
    }
}
