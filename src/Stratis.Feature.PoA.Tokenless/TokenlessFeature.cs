using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Behaviors;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Feature.PoA.Tokenless.Core;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessFeature : FullNodeFeature
    {
        private readonly ICoreComponent coreComponent;

        private readonly IPoAMiner miner;

        private readonly VotingManager votingManager;

        private readonly IWhitelistedHashesRepository whitelistedHashesRepository;

        private readonly IdleFederationMembersKicker idleFederationMembersKicker;

        private readonly CertificatesManager certificatesManager;

        private readonly RevocationChecker revocationChecker;

        public TokenlessFeature(
            ICoreComponent coreComponent,
            CertificatesManager certificatesManager,
            IdleFederationMembersKicker idleFederationMembersKicker,
            IPoAMiner miner,
            PayloadProvider payloadProvider,
            RevocationChecker revocationChecker,
            VotingManager votingManager,
            IWhitelistedHashesRepository whitelistedHashesRepository)
        {
            this.coreComponent = coreComponent;

            this.certificatesManager = certificatesManager;
            this.idleFederationMembersKicker = idleFederationMembersKicker;
            this.miner = miner;
            this.revocationChecker = revocationChecker;
            this.votingManager = votingManager;
            this.whitelistedHashesRepository = whitelistedHashesRepository;

            payloadProvider.DiscoverPayloads(this.GetType().Assembly);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.coreComponent.ConnectionManager.Parameters;

            this.ReplaceConsensusManagerBehavior(connectionParameters);

            this.ReplaceBlockStoreBehavior(connectionParameters);

            this.coreComponent.FederationManager.Initialize();
            this.whitelistedHashesRepository.Initialize();

            var options = (PoAConsensusOptions)this.coreComponent.Network.Consensus.Options;

            if (options.VotingEnabled)
            {
                this.votingManager.Initialize();

                if (options.AutoKickIdleMembers)
                    this.idleFederationMembersKicker.Initialize();
            }

            if (options.EnablePermissionedMembership)
            {
                await this.revocationChecker.InitializeAsync().ConfigureAwait(false);
                await this.certificatesManager.InitializeAsync().ConfigureAwait(false);
            }

            this.miner.InitializeMining();
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
            connectionParameters.TemplateBehaviors.Add(new PoAConsensusManagerBehavior(this.coreComponent.ChainIndexer, this.coreComponent.InitialBlockDownloadState, this.coreComponent.ConsensusManager, this.coreComponent.PeerBanning, this.coreComponent.LoggerFactory));
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
            connectionParameters.TemplateBehaviors.Add(new PoABlockStoreBehavior(this.coreComponent.ChainIndexer, this.coreComponent.ChainState, this.coreComponent.LoggerFactory, this.coreComponent.ConsensusManager, this.coreComponent.BlockStoreQueue));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.miner.Dispose();

            this.votingManager.Dispose();

            this.idleFederationMembersKicker.Dispose();

            if (((PoAConsensusOptions)this.coreComponent.Network.Consensus.Options).EnablePermissionedMembership)
            {
                this.revocationChecker.Dispose();
            }
        }
    }
}
