using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.AsyncWork;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA;
using Stratis.SmartContracts.Core.AccessControl;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary> Starts and stops system and standard channel nodes.</summary>
    public interface IChannelService
    {
        int GetDefaultAPIPort(int channelId);
        int GetDefaulPort(int channelId);
        int GetDefaultSignalRPort(int channelId);

        Task JoinChannelAsync(ChannelNetwork network);
        Task CreateAndStartChannelNodeAsync(ChannelCreationRequest request);
        Task StartSystemChannelNodeAsync();
        Task RestartChannelNodesAsync();
        void StopChannelNodes();

        void Initialize();
    }

    /// <inheritdoc />
    public abstract class ChannelService : IChannelService
    {
        public const int SystemChannelId = 1;
        protected const string SystemChannelName = "system";

        protected const string ChannelConfigurationFileName = "channel.conf";

        protected readonly IChannelRepository channelRepository;
        protected readonly ChannelSettings channelSettings;
        protected readonly IDateTimeProvider dateTimeProvider;
        protected readonly TokenlessKeyStoreSettings keyStoreSettings;
        protected readonly ILogger logger;
        protected readonly IMembershipServicesDirectory membershipServicesDirectory;
        protected readonly NodeSettings nodeSettings;
        protected IAsyncLoop terminationLoop;
        protected readonly TokenlessNetwork tokenlessNetworkDefaults;

        protected ChannelService(
            IChannelRepository channelRepository,
            ChannelSettings channelSettings,
            IDateTimeProvider dateTimeProvider,
            TokenlessKeyStoreSettings keyStoreSettings,
            ILoggerFactory loggerFactory,
            IMembershipServicesDirectory membershipServicesDirectory,
            NodeSettings nodeSettings)
        {
            this.channelSettings = channelSettings;
            this.dateTimeProvider = dateTimeProvider;
            this.keyStoreSettings = keyStoreSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.channelRepository = channelRepository;
            this.membershipServicesDirectory = membershipServicesDirectory;
            this.tokenlessNetworkDefaults = new TokenlessNetwork();
        }

        public abstract void Initialize();
        protected abstract Task<bool> StartChannelAsync(string channelRootFolder, params string[] channelArgs);
        public abstract void StopChannelNodes();

        public async Task CreateAndStartChannelNodeAsync(ChannelCreationRequest request)
        {
            try
            {
                this.logger.LogInformation($"Creating and starting a node on channel '{request.Name}'.");

                int channelNodeId = this.channelRepository.GetNextChannelId();

                string channelRootFolder = PrepareChannelNodeForStartup(request.Name, request.Identifier, channelNodeId, request.AccessList);

                bool started = await StartChannelAsync(channelRootFolder, $"-channelname={request.Name}");
                if (!started)
                    this.logger.LogWarning($"Failed to start node on channel '{request.Name}' as the process exited early.");

            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start node on channel '{request.Name}': {ex.Message}");
            }
        }

        public async Task JoinChannelAsync(ChannelNetwork network)
        {
            // Must be a "normal" node.
            if (this.channelSettings.IsChannelNode || this.channelSettings.IsInfraNode || this.channelSettings.IsSystemChannelNode)
            {
                throw new ChannelServiceException("Only normal nodes can process channel join requests.");
            }

            if (network.Id == 0)
                throw new ChannelServiceException("The network id can't be 0.");

            // Record channel membership (in normal node repo) and start up channel node.
            this.logger.LogInformation($"Joining and starting a node on channel '{network.Name}'.");

            string channelRootFolder = PrepareChannelNodeForStartup(network.Name, null, network.Id, network.InitialAccessList, network);

            bool started = await StartChannelAsync(channelRootFolder, $"-channelname={network.Name}");
            if (!started)
                this.logger.LogWarning($"Failed to start node on channel '{network.Name}' as the process exited early.");
        }

        public async Task RestartChannelNodesAsync()
        {
            Dictionary<string, ChannelDefinition> channels = this.channelRepository.GetChannelDefinitions();

            // Don't include the system channel node in this as it is explicitly started by the infra node.
            IEnumerable<ChannelDefinition> channelDefinitions = channels.Values.ToList().Where(c => c.Id != SystemChannelId);

            this.logger.LogInformation($"This node has {channelDefinitions.Count()} channels to start.");

            foreach (ChannelDefinition channel in channelDefinitions)
            {
                try
                {
                    this.logger.LogInformation($"Restarting a node on channel '{channel.Name}'.");

                    int channelNodeId = channel.Id;

                    string channelRootFolder = PrepareChannelNodeForStartup(channel.Name, null, channelNodeId, channel.AccessList);

                    bool started = await StartChannelAsync(channelRootFolder, $"-channelname={channel.Name}");
                    if (!started)
                        this.logger.LogWarning($"Failed to restart node on channel '{channel.Name}' as the process exited early.");
                }
                catch (Exception ex)
                {
                    throw new ChannelServiceException($"Failed to restart channel nodes: {ex.Message}");
                }
            }
        }

        public async Task StartSystemChannelNodeAsync()
        {
            try
            {
                this.logger.LogInformation("Starting a system channel node.");

                var channelRootFolder = Path.Combine(this.nodeSettings.DataFolder.RootPath, "channels", SystemChannelName.ToLowerInvariant());
                Directory.CreateDirectory(channelRootFolder);

                // Copy the parent node's authority and client certificate to the channel node's root.
                CopyCertificatesToChannelRoot(channelRootFolder);

                // Copy the parent node's key store files to the channel node's root.
                CopyKeyStoreToChannelRoot(channelRootFolder);

                var args = new string[] { "-bootstrap=1", $"-channelname={SystemChannelName}", "-issystemchannelnode=true" };

                if (!string.IsNullOrEmpty(this.channelSettings.SystemChannelApiUri))
                    args = args.Concat(new string[] { $"-apiuri={this.channelSettings.SystemChannelApiUri}" }).ToArray();

                if (this.channelSettings.SystemChannelApiPort != 0)
                    args = args.Concat(new string[] { $"-apiport={this.channelSettings.SystemChannelApiPort}" }).ToArray();

                if (!string.IsNullOrEmpty(this.channelSettings.SystemChannelProtocolUri))
                    args = args.Concat(new string[] { $"-bind={this.channelSettings.SystemChannelProtocolUri}" }).ToArray();

                if (this.channelSettings.SystemChannelProtocolPort != 0)
                    args = args.Concat(new string[] { $"-port={this.channelSettings.SystemChannelProtocolPort}" }).ToArray();

                bool started = await StartChannelAsync(channelRootFolder, args);
                if (!started)
                    throw new ChannelServiceException($"Failed to start system channel node as the process exited early.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start system channel node: {ex.Message}");
            }
        }

        /// <summary>
        /// This is only called for non system channel nodes.
        /// </summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <param name="channelId">The id of the channel.</param>
        /// <param name="organisation">The organisation the channel belongs to.</param>
        /// <param name="network">If this is from join channel request, the network will be provided.</param>
        /// <returns>The channel's network root folder.</returns>
        private string PrepareChannelNodeForStartup(string channelName, string channelIdentifier, int channelId, AccessControlList accessList, ChannelNetwork network = null)
        {
            // Write the serialized version of the network to disk.
            string channelRootFolder = WriteChannelNetworkJson(channelName, channelIdentifier, channelId, accessList, network);

            // Copy the parent node's authority and client certificate to the channel node's root.
            CopyCertificatesToChannelRoot(channelRootFolder);

            // Copy the parent node's key store files to the channel node's root.
            CopyKeyStoreToChannelRoot(channelRootFolder);

            return channelRootFolder;
        }

        /// <summary>Write the serialized network to disk.</summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <returns>The channel's root folder path.</returns>
        private string WriteChannelNetworkJson(string channelName, string channelIdentifier, int channelId, AccessControlList accessList, ChannelNetwork channelNetwork = null)
        {
            // If the network json already exist, do nothing.
            var rootFolderName = Path.Combine(this.nodeSettings.DataFolder.RootPath, "channels", channelName.ToLowerInvariant());
            var networkFileName = Path.Combine(rootFolderName, $"{channelName.ToLowerInvariant()}_network.json");
            if (File.Exists(networkFileName))
                return rootFolderName;

            if (channelNetwork == null)
            {
                channelNetwork = SystemChannelNetwork.CreateChannelNetwork(channelName.ToLowerInvariant(), channelIdentifier, rootFolderName, this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp());
                channelNetwork.Id = channelId;
                channelNetwork.InitialAccessList = accessList;
                channelNetwork.DefaultAPIPort = this.GetDefaultAPIPort(channelId);
                channelNetwork.DefaultPort = this.GetDefaulPort(channelId);
                channelNetwork.DefaultSignalRPort = this.GetDefaultSignalRPort(channelId);
            }
            else
            {
                Guard.Equals(channelName.ToLowerInvariant(), channelNetwork.Name);
                Guard.Equals(channelId, channelNetwork.Id);
            }

            var serializedJson = JsonSerializer.Serialize(channelNetwork);
            Directory.CreateDirectory(rootFolderName);
            File.WriteAllText(networkFileName, serializedJson);

            var channelDefinition = new ChannelDefinition()
            {
                Id = channelId,
                Name = channelName,
                NetworkJson = serializedJson,
                AccessList = accessList
            };

            this.channelRepository.SaveChannelDefinition(channelDefinition);

            return rootFolderName;
        }

        public int GetDefaultAPIPort(int channelId)
        {
            return this.tokenlessNetworkDefaults.DefaultAPIPort + channelId;
        }

        public int GetDefaulPort(int channelId)
        {
            return this.tokenlessNetworkDefaults.DefaultPort + channelId;
        }

        public int GetDefaultSignalRPort(int channelId)
        {
            return this.tokenlessNetworkDefaults.DefaultSignalRPort + channelId;
        }

        private void CopyCertificatesToChannelRoot(string channelRootFolder)
        {
            var tempMsd = new LocalMembershipServicesConfiguration(channelRootFolder, this.tokenlessNetworkDefaults);
            tempMsd.InitializeFolderStructure();

            // If the certificates already exist, do nothing.
            var authorityCertificatePath = Path.Combine(channelRootFolder, "msd", "cacerts", CertificateAuthorityInterface.AuthorityCertificateName);
            if (!File.Exists(authorityCertificatePath))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, "msd", "cacerts", CertificateAuthorityInterface.AuthorityCertificateName), authorityCertificatePath);

            // If the certificates already exist, do nothing.
            var clientCertificatePath = Path.Combine(channelRootFolder, CertificateAuthorityInterface.ClientCertificateName);
            if (!File.Exists(clientCertificatePath))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificateAuthorityInterface.ClientCertificateName), clientCertificatePath);
        }

        private void CopyKeyStoreToChannelRoot(string channelRootFolder)
        {
            var miningKeyFile = Path.Combine(channelRootFolder, KeyTool.FederationKeyFileName);
            if (!File.Exists(miningKeyFile))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, KeyTool.FederationKeyFileName), miningKeyFile);

            var transactionSigningKeyFile = Path.Combine(channelRootFolder, KeyTool.TransactionSigningKeyFileName);
            if (!File.Exists(transactionSigningKeyFile))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, KeyTool.TransactionSigningKeyFileName), transactionSigningKeyFile);

            var keyStoreFile = Path.Combine(channelRootFolder, TokenlessKeyStoreManager.KeyStoreFileName);
            if (!File.Exists(keyStoreFile))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, TokenlessKeyStoreManager.KeyStoreFileName), keyStoreFile);
        }

        protected void CreateChannelConfigurationFile(string channelRootFolder, params string[] channelArgs)
        {
            var configurationFilePath = Path.Combine(channelRootFolder, ChannelConfigurationFileName);

            var args = new StringBuilder();
            args.AppendLine($"-{CertificateAuthorityInterface.CaBaseUrlKey}={this.membershipServicesDirectory.CertificateAuthorityInterface.CaUrl}");
            args.AppendLine($"-{CertificateAuthorityInterface.CaAccountIdKey}={Settings.AdminAccountId}");
            args.AppendLine($"-{CertificateAuthorityInterface.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificateAuthorityInterface.CaPasswordKey, "")} ");
            args.AppendLine($"-{Settings.KeyStorePasswordKey}={this.keyStoreSettings.KeyStorePassword}");

            // Append any channel specific arguments.
            foreach (var channelArg in channelArgs)
            {
                args.AppendLine(channelArg);
            }

            File.WriteAllText(configurationFilePath, args.ToString());
        }
    }
}
