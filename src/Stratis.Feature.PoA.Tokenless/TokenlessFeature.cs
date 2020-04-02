using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.Behaviors;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.KeyStore;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessFeature : FullNodeFeature
    {
        private readonly ICoreComponent coreComponent;

        private readonly ICertificatesManager certificatesManager;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly VotingManager votingManager;
        private readonly IFederationManager federationManager;
        private readonly IPoAMiner miner;
        private readonly IRevocationChecker revocationChecker;
        private readonly NodeSettings nodeSettings;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger logger;
        private readonly IMembershipServicesDirectory membershipServices;
        private IAsyncLoop caPubKeysLoop;
        private readonly TokenlessKeyStoreSettings tokenlessKeyStoreSettings;

        // TODO-TL: Will move to channel start service or something.
        public List<int> StartedChannelNodes = new List<int>();

        public TokenlessFeature(
            ICertificatesManager certificatesManager,
            ICertificatePermissionsChecker certificatePermissionsChecker,
            VotingManager votingManager,
            ICoreComponent coreComponent,
            IFederationManager federationManager,
            IPoAMiner miner,
            PayloadProvider payloadProvider,
            IRevocationChecker revocationChecker,
            StoreSettings storeSettings,
            NodeSettings nodeSettings,
            TokenlessKeyStoreSettings tokenlessKeyStoreSettings,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IMembershipServicesDirectory membershipServices)
        {
            this.certificatesManager = certificatesManager;
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.votingManager = votingManager;
            this.coreComponent = coreComponent;
            this.federationManager = federationManager;
            this.miner = miner;
            this.revocationChecker = revocationChecker;
            this.nodeSettings = nodeSettings;
            this.tokenlessKeyStoreSettings = tokenlessKeyStoreSettings;
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.caPubKeysLoop = null;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.membershipServices = membershipServices;

            // TODO-TL: Is there a better place to do this?
            storeSettings.TxIndex = true;

            payloadProvider.DiscoverPayloads(typeof(PoAFeature).Assembly);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync()
        {
            NetworkPeerConnectionParameters connectionParameters = this.coreComponent.ConnectionManager.Parameters;

            this.ReplaceConsensusManagerBehavior(connectionParameters);

            this.ReplaceBlockStoreBehavior(connectionParameters);

            this.federationManager.Initialize();

            // TODO-TL: Check if we need a new ConsensusOptions.
            var options = (PoAConsensusOptions)this.coreComponent.Network.Consensus.Options;
            if (options.EnablePermissionedMembership)
            {
                this.membershipServices.Initialize();
                this.revocationChecker.Initialize();
                // We do not need to initialize the CertificatesManager here like it would have been in the regular PoA feature, because the TokenlessWalletManager is now responsible for ensuring a client certificate is created instead.
            }

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
                    this.logger.LogDebug(e, "Exception raised when calling CA to synchronize members.");
                    this.logger.LogWarning("Could not synchronize members with CA. CA is possibly down! Will retry in 1 minute.");
                }
            },
            this.nodeLifetime.ApplicationStopping,
            repeatEvery: TimeSpans.Minute,
            startAfter: TimeSpans.Minute);

            // If this node is a infra node, then start another daemon with the serialized version of the network.
            if (this.tokenlessKeyStoreSettings.IsInfraNode)
            {
                this.logger.LogInformation("InfraNode will attempt to start a system channel node.");
                this.StartedChannelNodes.Add(StartSystemChannelNode());
            }
        }

        private int StartSystemChannelNode()
        {
            // Write the serialized network to disk.
            var channelName = "system";
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, $"{this.nodeSettings.DataFolder.RootPath}\\channels\\{channelName.ToLowerInvariant()}");
            var serializedJson = JsonSerializer.Serialize(channelNetwork);
            Directory.CreateDirectory(channelNetwork.RootFolderName);

            var filePath = $"{channelNetwork.RootFolderName}\\{channelName}_network.json";
            File.WriteAllText(filePath, serializedJson);

            // Copy the parent node's configuration file (.conf) to the channel node's root.
            File.Copy(Path.Combine(this.nodeSettings.ConfigurationFile), Path.Combine(channelNetwork.RootFolderName, "poa.conf"));

            // Copy the parent node's authority and client certificate to the channel node's root.
            File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.AuthorityCertificateName), Path.Combine(channelNetwork.RootFolderName, CertificatesManager.AuthorityCertificateName));
            File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.ClientCertificateName), Path.Combine(channelNetwork.RootFolderName, CertificatesManager.ClientCertificateName));

            // Pass the path to the serialized network to the system channel node and start it.
            var process = new Process();
            process.StartInfo.WorkingDirectory = @"..\..\..\..\Stratis.TokenlessD\";
            process.StartInfo.FileName = "dotnet.exe";

            var args = new StringBuilder();
            args.Append("-apiport=20000 ");
            args.Append("-certificatepassword=test ");
            args.Append("-password=test ");
            args.Append("-conf=poa.conf ");
            args.Append($"-datadir={channelNetwork.RootFolderName} ");
            args.Append($"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId} ");
            args.Append($"-{CertificatesManager.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificatesManager.CaPasswordKey, "")} ");
            args.Append($"-{CertificatesManager.ClientCertificateConfigurationKey}=test ");
            args.Append("-ischannelnode=true ");
            args.Append("-isinfranode=false");

            process.StartInfo.Arguments = $"run --no-build {args.ToString()}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

            Guard.Assert(!process.HasExited);

            return process.Id;
        }

        private void SynchronizeMembers()
        {
            // If we're not a federation member, it's not our job to vote. Don't schedule any votes until we are one.
            if (!this.federationManager.IsFederationMember)
            {
                this.logger.LogDebug("Attempted to Synchronize members but we aren't a member yet.");
                return;
            }

            List<PubKey> allowedMembers = this.certificatesManager.GetCertificatePublicKeys();
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
            connectionParameters.TemplateBehaviors.Add(new RevocationBehavior(this.coreComponent.Network, this.coreComponent.LoggerFactory, this.revocationChecker));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.caPubKeysLoop?.Dispose();

            this.miner.Dispose();

            this.votingManager.Dispose();

            if (((PoAConsensusOptions)this.coreComponent.Network.Consensus.Options).EnablePermissionedMembership)
            {
                this.revocationChecker.Dispose();
            }

            foreach (var channelNodePId in this.StartedChannelNodes)
            {
                var process = Process.GetProcessById(channelNodePId);

                // TODO-TL: Need to gracefully shutdown
                process.Kill();
                //process.CloseMainWindow();
                //process.WaitForExit();
            }
        }
    }
}
