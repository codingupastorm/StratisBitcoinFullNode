using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.PoA;
using Stratis.Core.AsyncWork;
using Stratis.Core.Builder.Feature;
using Stratis.Core.Consensus;
using Stratis.Core.P2P.Peer;
using Stratis.Core.P2P.Protocol.Behaviors;
using Stratis.Core.P2P.Protocol.Payloads;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.BlockStore;
using Stratis.Features.PoA;
using Stratis.Features.PoA.Behaviors;
using Stratis.Features.PoA.Voting;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessFeature : FullNodeFeature
    {
        private readonly ICoreComponent coreComponent;

        private readonly ChannelSettings channelSettings;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly VotingManager votingManager;
        private readonly IFederationManager federationManager;
        private readonly IEndorsementRequestHandler requestHandler;
        private readonly IEndorsementSuccessHandler successHandler;
        private readonly IPoAMiner miner;
        private readonly IAsyncProvider asyncProvider;
        private readonly ITransientStore transientStore;
        private readonly IPrivateDataStore privateDataStore;
        private readonly ILogger logger;
        private readonly IMembershipServicesDirectory membershipServices;
        private IAsyncLoop caPubKeysLoop;
        private readonly IChannelService channelService;
        private readonly IChannelCreationExecutor channelCreationExecutor;
        private readonly IChannelUpdateExecutor channelUpdateExecutor;
        private readonly ReadWriteSetPolicyValidator rwsPolicyValidator;

        public TokenlessFeature(
            ChannelSettings channelSettings,
            ICertificatePermissionsChecker certificatePermissionsChecker,
            VotingManager votingManager,
            ICoreComponent coreComponent,
            IFederationManager federationManager,
            IEndorsementRequestHandler requestHandler,
            IEndorsementSuccessHandler successHandler,
            IPoAMiner miner,
            PayloadProvider payloadProvider,
            StoreSettings storeSettings,
            IAsyncProvider asyncProvider,
            IMembershipServicesDirectory membershipServices,
            ITransientStore transientStore,
            IPrivateDataStore privateDataStore,
            IChannelService channelService,
            IChannelCreationExecutor channelCreationExecutor,
            IChannelUpdateExecutor channelUpdateExecutor,
            ReadWriteSetPolicyValidator rwsPolicyValidator)
        {
            this.channelSettings = channelSettings;
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.votingManager = votingManager;
            this.coreComponent = coreComponent;
            this.federationManager = federationManager;
            this.requestHandler = requestHandler;
            this.successHandler = successHandler;
            this.miner = miner;
            this.asyncProvider = asyncProvider;
            this.transientStore = transientStore;
            this.privateDataStore = privateDataStore;
            this.logger = this.coreComponent.LoggerFactory.CreateLogger(this.GetType().FullName);
            this.membershipServices = membershipServices;
            this.channelService = channelService;
            this.rwsPolicyValidator = rwsPolicyValidator;

            this.channelCreationExecutor = channelCreationExecutor;
            this.channelUpdateExecutor = channelUpdateExecutor;

            // TODO-TL: Is there a better place to do this?
            storeSettings.TxIndex = true;

            payloadProvider.DiscoverPayloads(typeof(PoAFeature).Assembly);
            payloadProvider.DiscoverPayloads(this.GetType().Assembly);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.coreComponent.ConnectionManager.Parameters;

            this.ReplaceConsensusManagerBehavior(connectionParameters);

            this.ReplaceBlockStoreBehavior(connectionParameters);

            connectionParameters.TemplateBehaviors.Add(new EndorsementRequestBehavior(this.requestHandler));
            connectionParameters.TemplateBehaviors.Add(new EndorsementSuccessBehavior(this.successHandler));
            connectionParameters.TemplateBehaviors.Add(new ReceivePrivateDataBehavior(this.transientStore, this.privateDataStore));
            connectionParameters.TemplateBehaviors.Add(new PrivateDataRequestBehavior(this.transientStore, this.rwsPolicyValidator));

            this.federationManager.Initialize();

            this.membershipServices.Initialize();

            // TODO-TL: Check if we need a new ConsensusOptions.
            var options = (PoAConsensusOptions)this.coreComponent.Network.Consensus.Options;

            if (options.VotingEnabled)
            {
                this.votingManager.Initialize();
            }

            // Check the local node's certificate for the mining permission.
            if (this.certificatePermissionsChecker.CheckOwnCertificatePermission(CaCertificatesManager.MiningPermissionOid))
                this.miner.InitializeMining();

            // Initialize the CA public key / federation member voting loop.
            this.caPubKeysLoop = this.asyncProvider.CreateAndRunAsyncLoop("PeriodicCAKeys", async (cancellation) =>
            {
                try
                {
                    this.SynchronizeMembers();
                }
                catch (Exception e)
                {
                    this.logger.LogWarning("Could not synchronize members, contacting the CA Server failed:", e.Message);
                }
            },
            this.coreComponent.NodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpan.FromSeconds(10),
            startAfter: TimeSpan.FromSeconds(30));

            this.channelService.Initialize();

            // If this node is a infra node, then start a system channel node daemon with the serialized version of the network.
            if (this.channelSettings.IsInfraNode)
                await this.channelService.StartSystemChannelNodeAsync();

            if (this.channelSettings.IsSystemChannelNode)
            {
                this.channelCreationExecutor.Initialize();
                this.channelUpdateExecutor.Initialize();
            }

            // Restart any channels that were created previously or that this nodes belong to.
            await this.channelService.RestartChannelNodesAsync();
        }

        private void SynchronizeMembers()
        {
            // If we're not a federation member, it's not our job to vote. Don't schedule any votes until we are one.
            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogDebug("Attempted to synchronize members but we aren't a member yet.");
                return;
            }

            List<PubKey> allowedMembers = this.membershipServices.GetCertificatePublicKeys();
            List<IFederationMember> currentMembers = this.federationManager.GetFederationMembers();

            // Check for differences and kick members without valid certificates.                
            IEnumerable<(VoteKey, IFederationMember)> requiredKickVotes = currentMembers
                .Where(m => !allowedMembers.Any(pk => pk == m.PubKey))
                .Select(m => (VoteKey.KickFederationMember, m));

            // Check for differences and add members with valid certificates.                
            IEnumerable<(VoteKey, IFederationMember)> requiredAddVotes = allowedMembers
                .Where(pk => !currentMembers.Any(a => a.PubKey == pk))
                .Select(pk => (VoteKey.AddFederationMember, (IFederationMember)(new FederationMember(pk))));

            List<VotingData> existingVotes = this.votingManager.GetScheduledVotes();
            var comparer = new ByteArrayComparer();

            // Schedule the votes.
            foreach ((VoteKey voteKey, IFederationMember federationMember) in requiredKickVotes.Concat(requiredAddVotes))
            {
                byte[] fedMemberBytes = (this.coreComponent.Network.Consensus.ConsensusFactory as PoAConsensusFactory).SerializeFederationMember(federationMember);

                // Don't schedule votes that are already scheduled.
                if (existingVotes.Any(e => e.Key == voteKey && comparer.Equals(e.Data, fedMemberBytes)))
                    continue;

                this.votingManager.ScheduleVote(new VotingData()
                {
                    Key = voteKey,
                    Data = fedMemberBytes
                });
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
            connectionParameters.TemplateBehaviors.Add(new PoAConsensusManagerBehavior(this.coreComponent.ChainIndexer, this.coreComponent.InitialBlockDownloadState, this.coreComponent.ConsensusManager, this.coreComponent.PeerBanning, this.coreComponent.LoggerFactory));
        }

        /// <summary>Replaces default <see cref="BlockStoreBehavior"/> with <see cref="PoABlockStoreBehavior"/>.</summary>
        private void ReplaceBlockStoreBehavior(NetworkPeerConnectionParameters connectionParameters)
        {
            INetworkPeerBehavior defaultBlockStoreBehavior = connectionParameters.TemplateBehaviors.FirstOrDefault(behavior => behavior is BlockStoreBehavior);

            if (defaultBlockStoreBehavior == null)
            {
                throw new MissingServiceException(typeof(BlockStoreBehavior), "Missing expected BlockStoreBehavior.");
            }

            connectionParameters.TemplateBehaviors.Remove(defaultBlockStoreBehavior);
            connectionParameters.TemplateBehaviors.Add(new PoABlockStoreBehavior(this.coreComponent.ChainIndexer, this.coreComponent.ChainState, this.coreComponent.LoggerFactory, this.coreComponent.ConsensusManager, this.coreComponent.BlockStoreQueue));
            connectionParameters.TemplateBehaviors.Add(new RevocationBehavior(this.coreComponent.Network, this.coreComponent.LoggerFactory, this.membershipServices));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.caPubKeysLoop?.Dispose();

            this.miner.Dispose();

            this.votingManager.Dispose();

            this.channelService.StopChannelNodes();
        }
    }
}
