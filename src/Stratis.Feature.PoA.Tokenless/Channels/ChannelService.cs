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
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary> Starts and stops system and standard channel nodes.</summary>
    public interface IChannelService
    {
        /// <summary>This is a list of PIds of channel processes that are running.</summary>
        List<ChannelNodeProcess> StartedChannelNodes { get; }

        Task StartChannelNodeAsync(ChannelCreationRequest request);
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
        public const int MillisecondsBeforeForcedKill = 10000;

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
            this.Pipe.Dispose();
            if (!this.Process.WaitForExit(MillisecondsBeforeForcedKill))
                this.Process.Kill();
        }
    }

    /// <inheritdoc />
    public sealed class ChannelService : IChannelService
    {
        private const string ChannelConfigurationFileName = "channel.conf";
        private const string SystemChannelName = "system";

        private readonly ChannelSettings channelSettings;
        private readonly ILogger logger;
        private readonly NodeSettings nodeSettings;
        private readonly IChannelRepository channelRepository;
        private readonly IAsyncProvider asyncProvider;

        private IAsyncLoop terminationLoop;

        /// <inheritdoc />
        public List<ChannelNodeProcess> StartedChannelNodes { get; }
        public INodeLifetime NodeLifetime { get; private set; }

        public ChannelService(ChannelSettings channelSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IChannelRepository channelRepository, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider)
        {
            this.channelSettings = channelSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.StartedChannelNodes = new List<ChannelNodeProcess>();
            this.channelRepository = channelRepository;
            this.NodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
        }

        public void Initialize()
        {
            this.logger.LogDebug($"ChannelParentPipeName = '{this.channelSettings.ChannelParentPipeName}'.");

            if (this.channelSettings.ChannelParentPipeName != null)
            {
                this.terminationLoop = this.asyncProvider.CreateAndRunAsyncLoop($"{nameof(ChannelService)}.TerminationLoop", token =>
                {
                    this.logger.LogDebug($"Connecting to parent on pipe '{this.channelSettings.ChannelParentPipeName}'.");

                    using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", this.channelSettings.ChannelParentPipeName, PipeDirection.In))
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


                    this.logger.LogDebug("Parent pipe broken. Stopping application.");
                    this.NodeLifetime.StopApplication();

                    return Task.CompletedTask;
                },
                this.NodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpan.FromSeconds(1));
            }
        }

        public async Task StartChannelNodeAsync(ChannelCreationRequest request)
        {
            try
            {
                this.logger.LogInformation($"Starting a node on channel '{request.Name}'.");

                int channelNodeId = this.channelRepository.GetNextChannelId();

                ChannelNodeProcess channelNode = await StartNodeAsync(request.Name, channelNodeId, "-ischannelnode=true");
                if (channelNode.Process.HasExited)
                    this.logger.LogWarning($"Failed to start node on channel '{request.Name}' as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(channelNode);

                var channelDefinition = new ChannelDefinition()
                {
                    Id = channelNodeId,
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
            Dictionary<string, ChannelDefinition> channels = this.channelRepository.GetChannelDefinitions();
            var channelDefinitions = channels.Keys.ToList();
            this.logger.LogInformation($"This node has {channelDefinitions.Count} to start.");

            foreach (var channel in channelDefinitions)
            {
                try
                {
                    this.logger.LogInformation($"Restarting a node on channel '{channel}'.");

                    int channelNodeId = channels[channel].Id;

                    ChannelNodeProcess channelNode = await StartNodeAsync(channel, channelNodeId, "-ischannelnode=true", $"-channelname={channel}");
                    if (channelNode.Process.HasExited)
                        this.logger.LogWarning($"Failed to restart node on channel '{channel}' as the process exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(channelNode);

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

                // If the node we are starting is a system channel node, then its API port offset will always be 1.
                int channelNodeId = 1;

                ChannelNodeProcess channelNode = await StartNodeAsync(SystemChannelName, channelNodeId, $"-channelname={SystemChannelName}", "-issystemchannelnode=true", "-ischannelnode=true");
                if (channelNode.Process.HasExited)
                    throw new ChannelServiceException($"Failed to start system channel node as the processs exited early.");

                lock (this.StartedChannelNodes)
                    this.StartedChannelNodes.Add(channelNode);

                this.logger.LogInformation("System channel node started.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start system channel node: {ex.Message}");
            }
        }

        private async Task<ChannelNodeProcess> StartNodeAsync(string channelName, int channelId, params string[] channelArgs)
        {
            // Write the serialized version of the network to disk.
            string channelRootFolder = WriteChannelNetworkJson(channelName, channelId);

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
        private string WriteChannelNetworkJson(string channelName, int channelId)
        {
            // If the network json already exist, do nothing.
            var rootFolderName = $"{this.nodeSettings.DataFolder.RootPath}\\channels\\{channelName.ToLowerInvariant()}";
            var networkFileName = $"{rootFolderName}\\{channelName}_network.json";
            if (File.Exists(networkFileName))
                return rootFolderName;

            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, rootFolderName);

            int portOffset = channelId;

            channelNetwork.DefaultAPIPort += portOffset;
            channelNetwork.DefaultPort += portOffset;
            channelNetwork.DefaultSignalRPort += portOffset;

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

        private async Task<ChannelNodeProcess> StartTheProcessAsync(string channelRootFolder)
        {
            var channelNodeProcess = new ChannelNodeProcess();
            var startUpArgs = $"run --no-build -nowarn -channelparentpipename={channelNodeProcess.PipeName} -conf={ChannelConfigurationFileName} -datadir={channelRootFolder}";
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
        }
    }
}
