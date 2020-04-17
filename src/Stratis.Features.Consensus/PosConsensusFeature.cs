﻿using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Signals;
using Stratis.Features.Consensus.Behaviors;

[assembly: InternalsVisibleTo("Stratis.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Features.Consensus.Tests")]

namespace Stratis.Features.Consensus
{
    public class PosConsensusFeature : ConsensusFeature
    {
        private readonly Network network;
        private readonly IChainState chainState;
        private readonly IConnectionManager connectionManager;
        private readonly IConsensusManager consensusManager;
        private readonly NodeDeployments nodeDeployments;
        private readonly ChainIndexer chainIndexer;
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;
        private readonly ICheckpoints checkpoints;
        private readonly IProvenBlockHeaderStore provenBlockHeaderStore;
        private readonly ConnectionManagerSettings connectionManagerSettings;

        public PosConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments,
            ChainIndexer chainIndexer,
            IInitialBlockDownloadState initialBlockDownloadState,
            IPeerBanning peerBanning,
            ISignals signals,
            ILoggerFactory loggerFactory,
            ICheckpoints checkpoints,
            IProvenBlockHeaderStore provenBlockHeaderStore,
            ConnectionManagerSettings connectionManagerSettings) : base(network, chainState, connectionManager, consensusManager, nodeDeployments)
        {
            this.network = network;
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;
            this.chainIndexer = chainIndexer;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.checkpoints = checkpoints;
            this.provenBlockHeaderStore = provenBlockHeaderStore;
            this.connectionManagerSettings = connectionManagerSettings;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;
        }

        /// <summary>
        /// Prints command-line help. Invoked via reflection.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static new void PrintHelp(Network network)
        {
            ConsensusFeature.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration. Invoked via reflection.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static new void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ConsensusFeature.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;

            var defaultConsensusManagerBehavior = connectionParameters.TemplateBehaviors.FirstOrDefault(behavior => behavior is ConsensusManagerBehavior);
            if (defaultConsensusManagerBehavior == null)
            {
                throw new MissingServiceException(typeof(ConsensusManagerBehavior), "Missing expected ConsensusManagerBehavior.");
            }

            // Replace default ConsensusManagerBehavior with ProvenHeadersConsensusManagerBehavior
            connectionParameters.TemplateBehaviors.Remove(defaultConsensusManagerBehavior);
            connectionParameters.TemplateBehaviors.Add(new ProvenHeadersConsensusManagerBehavior(this.chainIndexer, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory, this.network, this.chainState, this.checkpoints, this.provenBlockHeaderStore, this.connectionManagerSettings));

            connectionParameters.TemplateBehaviors.Add(new ProvenHeadersReservedSlotsBehavior(this.connectionManager, this.loggerFactory));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }
}