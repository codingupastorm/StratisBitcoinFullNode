using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary> Starts and stops system and standard channel nodes.</summary>
    public interface IChannelService
    {
        /// <summary>This is a list of PIds of channel processes that are running.</summary>
        List<int> StartedChannelNodes { get; }

        Task StartChannelNodeAsync(ChannelCreationRequest request);
        Task StartSystemChannelNodeAsync();
        Task RestartChannelNodesAsync();
    }

    /// <inheritdoc />
    public sealed class ChannelService : IChannelService
    {
        private const string ChannelConfigurationFileName = "channel.conf";
        private const int SystemChannelApiPort = 30001;
        private const string SystemChannelName = "system";

        private readonly ChannelSettings channelSettings;
        private readonly ILogger logger;
        private readonly NodeSettings nodeSettings;
        private readonly IChannelRepository channelRepository;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncProvider asyncProvider;

        private IAsyncLoop terminationLoop;

        /// <inheritdoc />
        public List<int> StartedChannelNodes { get; }

        public ChannelService(ChannelSettings channelSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IChannelRepository channelRepository, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider)
        {
            this.channelSettings = channelSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.StartedChannelNodes = new List<int>();
            this.channelRepository = channelRepository;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            if (this.channelSettings.ChannelParentPID != 0)
            {
                this.terminationLoop = this.asyncProvider.CreateAndRunAsyncLoop($"{nameof(ChannelService)}.TerminationLoop", token =>
                {
                    if (Process.GetProcessById(this.channelSettings.ChannelParentPID) == null)
                        this.nodeLifetime.StopApplication();

                    return Task.CompletedTask;
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromSeconds(1), // Weird number to prevent collisions with some other periodic console outputs.
                startAfter: TimeSpans.FiveSeconds);
            }
        }

        public async Task StartChannelNodeAsync(ChannelCreationRequest request)
        {
            try
            {
                this.logger.LogInformation($"Starting a node on channel '{request.Name}'.");

                Process process = await StartNodeAsync(request.Name, "-ischannelnode=true");
                if (process.HasExited)
                    this.logger.LogWarning($"Failed to start node on channel '{request.Name}' as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(process.Id);

                var channelDefinition = new ChannelDefinition()
                {
                    Name = request.Name
                };

                this.channelRepository.SaveChannelDefinition(channelDefinition);

                this.logger.LogInformation($"Node started on channel '{request.Name}'.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start node on channel '{request.Name}': {ex.Message}");
            }
        }

        public async Task RestartChannelNodesAsync()
        {
            var channelDefinitions = this.channelRepository.GetChannelDefinitions().Keys.ToList();
            this.logger.LogInformation($"This node has {channelDefinitions.Count} to start.");

            foreach (var channel in channelDefinitions)
            {
                try
                {
                    this.logger.LogInformation($"Restarting a node on channel '{channel}'.");

                    Process process = await StartNodeAsync(channel, "-ischannelnode=true", $"-channelname={channel}");
                    if (process.HasExited)
                        this.logger.LogWarning($"Failed to restart node on channel '{channel}' as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(process.Id);

                    this.logger.LogInformation($"Node restarted on channel '{channel}'.");
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

                Process process = await StartNodeAsync(SystemChannelName, $"-channelname={SystemChannelName}", "-issystemchannelnode=true", "-ischannelnode=true");
                if (process.HasExited)
                    throw new ChannelServiceException($"Failed to start system channel node as the processs exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(process.Id);

                this.logger.LogInformation("System channel node started.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start system channel node: {ex.Message}");
            }
        }

        private async Task<Process> StartNodeAsync(string channelName, params string[] channelArgs)
        {
            // Write the serialized version of the network to disk.
            string channelRootFolder = WriteChannelNetworkJson(channelName);

            // Copy the parent node's authority and client certificate to the channel node's root.
            CopyCertificatesToChannelRoot(channelRootFolder);

            // Create channel configuration file.
            CreateChannelConfigurationFile(channelRootFolder, channelArgs);

            // Pass the path to the serialized network to the system channel node and start it.
            return await StartTheProcessAsync(channelRootFolder);
        }

        /// <summary>Write the serialized network to disk.</summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <returns>The channel's root folder path.</returns>
        private string WriteChannelNetworkJson(string channelName)
        {
            // If the network json already exist, do nothing.
            var rootFolderName = $"{this.nodeSettings.DataFolder.RootPath}\\channels\\{channelName.ToLowerInvariant()}";
            var networkFileName = $"{rootFolderName}\\{channelName}_network.json";
            if (File.Exists(networkFileName))
                return rootFolderName;

            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, rootFolderName);

            // If the node we are starting is a system channel node, then its API port will always be 30001
            if (channelName == SystemChannelName)
                channelNetwork.DefaultAPIPort = SystemChannelApiPort;

            var serializedJson = JsonSerializer.Serialize(channelNetwork);
            Directory.CreateDirectory(rootFolderName);
            File.WriteAllText(networkFileName, serializedJson);
            return rootFolderName;
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

        private void CreateChannelConfigurationFile(string channelRootFolder, params string[] channelArgs)
        {
            // If the configuration file already exist, do nothing.
            var configurationFilePath = Path.Combine(channelRootFolder, ChannelConfigurationFileName);
            if (File.Exists(configurationFilePath))
                return;

            var args = new StringBuilder();
            args.AppendLine($"-certificatepassword=test");
            args.AppendLine($"-password=test");
            args.AppendLine($"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId}");
            args.AppendLine($"-{CertificatesManager.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificatesManager.CaPasswordKey, "")} ");
            args.AppendLine($"-{CertificatesManager.ClientCertificateConfigurationKey}=test");

            // Append any channel specific arguments.
            foreach (var channelArg in channelArgs)
            {
                args.AppendLine(channelArg);
            }

            File.WriteAllText(configurationFilePath, args.ToString());
        }

        private async Task<Process> StartTheProcessAsync(string channelRootFolder)
        {
            Process parentProcess = Process.GetCurrentProcess();
            var startUpArgs = $"run --no-build -nowarn -channelparentpid={parentProcess.Id} -conf={ChannelConfigurationFileName} -datadir={channelRootFolder}";
            this.logger.LogInformation($"Attempting to start process with args '{startUpArgs}'");

            var process = new Process();
            process.StartInfo.WorkingDirectory = this.channelSettings.ProcessPath;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = startUpArgs;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            this.logger.LogInformation("Executing a delay to wait for the node to start.");
            await Task.Delay(TimeSpan.FromSeconds(10));

            return process;
        }
    }
}
