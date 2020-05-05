using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.PoA;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary> Starts and stops system and standard channel nodes.</summary>
    public interface IChannelService
    {
        /// <summary>This is a list of PIds of channel processes that are running.</summary>
        List<ChannelNodeProcess> StartedChannelNodes { get; }

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

    /// <summary>
    /// Server-side class representing a channel process.
    /// </summary>
    public class ChannelNodeProcess : IDisposable
    {
        public const int MillisecondsBeforeForcedKill = 20000;

        public string PipeName { get; private set; }
        public NamedPipeServerStream Pipe { get; private set; }
        public Process Process { get; private set; }

        public ChannelNodeProcess()
        {
            // The child will use the validity of this pipe to decide whether to terminate.
            this.PipeName = (Guid.NewGuid()).ToString().Replace("-", "");
            this.Pipe = new NamedPipeServerStream(this.PipeName, PipeDirection.Out);

            this.Process = new Process();
        }

        public void Dispose()
        {
            // This will break the pipe so that the child node will be prompted to shut itself down.
            this.Pipe.Dispose();

            if (!this.Process.WaitForExit(MillisecondsBeforeForcedKill))
                this.Process.Kill();
        }
    }

    /// <inheritdoc />
    public sealed class ChannelService : IChannelService
    {
        public const int SystemChannelId = 1;
        private const string SystemChannelName = "system";
        private const string SystemChannelOrganisation = "";

        private const string ChannelConfigurationFileName = "channel.conf";

        private readonly IAsyncProvider asyncProvider;
        private readonly IChannelRepository channelRepository;
        private readonly ChannelSettings channelSettings;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly NodeSettings nodeSettings;
        private IAsyncLoop terminationLoop;
        private readonly TokenlessNetwork tokenlessNetworkDefaults;

        /// <inheritdoc />
        public List<ChannelNodeProcess> StartedChannelNodes { get; }

        public ChannelService(ChannelSettings channelSettings, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IChannelRepository channelRepository, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider)
        {
            this.channelSettings = channelSettings;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.channelRepository = channelRepository;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
            this.tokenlessNetworkDefaults = new TokenlessNetwork();

            this.StartedChannelNodes = new List<ChannelNodeProcess>();
        }

        public void Initialize()
        {
            this.logger.LogInformation($"ChannelParentPipeName = '{this.channelSettings.ChannelParentPipeName}'.");

            if (this.channelSettings.ChannelParentPipeName != null)
            {
                this.terminationLoop = this.asyncProvider.CreateAndRunAsyncLoop($"{nameof(ChannelService)}.TerminationLoop", token =>
                {
                    this.logger.LogInformation($"Connecting to parent on pipe '{this.channelSettings.ChannelParentPipeName}'.");

                    using (var pipe = new NamedPipeClientStream(".", this.channelSettings.ChannelParentPipeName, PipeDirection.In))
                    {
                        try
                        {
                            pipe.Connect();
                            pipe.Read(new byte[1], 0, 1);
                        }
                        catch (Exception)
                        {
                        }
                    }

                    this.logger.LogInformation("Parent pipe broken. Stopping application.");
                    this.nodeLifetime.StopApplication();

                    return Task.CompletedTask;
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromSeconds(1));
            }
        }

        public async Task CreateAndStartChannelNodeAsync(ChannelCreationRequest request)
        {
            try
            {
                this.logger.LogInformation($"Creating and starting a node on channel '{request.Name}'.");

                int channelNodeId = this.channelRepository.GetNextChannelId();

                string channelRootFolder = PrepareNodeForStartup(request.Name, channelNodeId, request.Organisation);

                ChannelNodeProcess channelNode = await StartTheProcessAsync(channelRootFolder, $"-channelname={request.Name}");
                if (channelNode.Process.HasExited)
                    this.logger.LogWarning($"Failed to start node on channel '{request.Name}' as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(channelNode);

                this.logger.LogInformation($"Node started on channel '{request.Name}' with Pid '{channelNode.Process.Id}'.");
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

            string channelRootFolder = PrepareNodeForStartup(network.Name, network.Id, network.Organisation, network);

            ChannelNodeProcess channelNode = await StartTheProcessAsync(channelRootFolder, $"-channelname={network.Name}");
            if (channelNode.Process.HasExited)
                this.logger.LogWarning($"Failed to start node on channel '{network.Name}' as the process exited early.");

            lock (this.StartedChannelNodes)
                this.StartedChannelNodes.Add(channelNode);

            this.logger.LogInformation($"Node started on channel '{network.Name}' with Pid '{channelNode.Process.Id}'.");
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
                    string organisation = channel.Organisation;

                    string channelRootFolder = PrepareNodeForStartup(channel.Name, channelNodeId, organisation);

                    ChannelNodeProcess channelNode = await StartTheProcessAsync(channelRootFolder, $"-channelname={channel.Name}");
                    if (channelNode.Process.HasExited)
                        this.logger.LogWarning($"Failed to restart node on channel '{channel.Name}' as the process exited early.");

                    lock (this.StartedChannelNodes)
                        this.StartedChannelNodes.Add(channelNode);

                    this.logger.LogInformation($"Node restarted on channel '{channel.Name}' with Pid '{channelNode.Process.Id}'.");
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

                string channelRootFolder = PrepareNodeForStartup(SystemChannelName, SystemChannelId, SystemChannelOrganisation);

                ChannelNodeProcess channelNode = await StartTheProcessAsync(channelRootFolder, "-bootstrap=1", $"-channelname={SystemChannelName}", "-issystemchannelnode=true");
                if (channelNode.Process.HasExited)
                    throw new ChannelServiceException($"Failed to start system channel node as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(channelNode);

                this.logger.LogInformation($"System channel node started with Pid '{channelNode.Process.Id}'.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start system channel node: {ex.Message}");
            }
        }

        private string PrepareNodeForStartup(string channelName, int channelId, string organisation, ChannelNetwork network = null)
        {
            // Write the serialized version of the network to disk.
            string channelRootFolder = WriteChannelNetworkJson(channelName, channelId, organisation, network);

            // Copy the parent node's authority and client certificate to the channel node's root.
            CopyCertificatesToChannelRoot(channelRootFolder);

            // Copy the parent node's key store files to the channel node's root.
            CopyKeyStoreToChannelRoot(channelRootFolder);

            return channelRootFolder;
        }

        /// <summary>Write the serialized network to disk.</summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <returns>The channel's root folder path.</returns>
        private string WriteChannelNetworkJson(string channelName, int channelId, string organisation, ChannelNetwork channelNetwork = null)
        {
            // If the network json already exist, do nothing.
            var rootFolderName = $"{this.nodeSettings.DataFolder.RootPath}\\channels\\{channelName.ToLowerInvariant()}";
            var networkFileName = $"{rootFolderName}\\{channelName.ToLowerInvariant()}_network.json";
            if (File.Exists(networkFileName))
                return rootFolderName;

            if (channelNetwork == null)
            {
                channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName.ToLowerInvariant(), rootFolderName, this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp());
                channelNetwork.Id = channelId;
                channelNetwork.Organisation = organisation;
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
                Organisation = organisation
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
            // If the certificates already exist, do nothing.
            var authorityCertificatePath = Path.Combine(channelRootFolder, CertificatesManager.AuthorityCertificateName);
            if (!File.Exists(authorityCertificatePath))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.AuthorityCertificateName), authorityCertificatePath);

            // If the certificates already exist, do nothing.
            var clientCertificatePath = Path.Combine(channelRootFolder, CertificatesManager.ClientCertificateName);
            if (!File.Exists(clientCertificatePath))
                File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.ClientCertificateName), clientCertificatePath);
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

        private void CreateChannelConfigurationFile(string channelRootFolder, params string[] channelArgs)
        {
            var configurationFilePath = Path.Combine(channelRootFolder, ChannelConfigurationFileName);

            var args = new StringBuilder();
            args.AppendLine($"-certificatepassword=test");
            args.AppendLine($"-password=test");
            args.AppendLine($"-{CertificatesManager.CaBaseUrlKey}={CertificatesManager.CaBaseUrl}");
            args.AppendLine($"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId}");
            args.AppendLine($"-{CertificatesManager.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificatesManager.CaPasswordKey, "")} ");
            args.AppendLine($"-{CertificatesManager.ClientCertificateConfigurationKey}=test");
            args.AppendLine($"-agent{CertificatesManager.ClientCertificateConfigurationKey}=test");

            // Append any channel specific arguments.
            foreach (var channelArg in channelArgs)
            {
                args.AppendLine(channelArg);
            }

            File.WriteAllText(configurationFilePath, args.ToString());
        }

        private async Task<ChannelNodeProcess> StartTheProcessAsync(string channelRootFolder, params string[] channelArgs)
        {
            var channelNodeProcess = new ChannelNodeProcess();

            // Create channel configuration file.
            if (this.nodeSettings.DebugMode)
            {
                this.logger.LogInformation($"Starting daemon in debug mode.");
                channelArgs = channelArgs.Concat(new[] { "-debug=1" }).ToArray();
            }

            CreateChannelConfigurationFile(channelRootFolder, channelArgs.Concat(new[] { "-ischannelnode=true", $"-channelparentpipename={channelNodeProcess.PipeName}" }).ToArray());

            var startUpArgs = $"run --no-build -nowarn -conf={ChannelConfigurationFileName} -datadir={channelRootFolder}";
            this.logger.LogInformation($"Attempting to start process with args '{startUpArgs}'");

            Process process = channelNodeProcess.Process;
            process.StartInfo.WorkingDirectory = this.channelSettings.ProcessPath;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = startUpArgs;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            this.logger.LogInformation("Executing a delay to wait for the node to start.");
            await Task.Delay(TimeSpan.FromSeconds(10));

            return channelNodeProcess;
        }

        public void StopChannelNodes()
        {
            Parallel.ForEach(this.StartedChannelNodes, (channelNode) =>
            {
                int pid = channelNode.Process.Id;

                this.logger.LogInformation($"Stopping channel node with PId: {pid}.");

                channelNode.Dispose();

                this.logger.LogInformation($"Stopped channel node with PId: {pid}.");
            });

            this.terminationLoop?.Dispose();
        }
    }
}
