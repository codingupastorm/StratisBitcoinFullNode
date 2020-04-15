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
using Stratis.Bitcoin.Configuration;
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
        void StopChannelNodes();
        void RestartChannelNodes();
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

        /// <inheritdoc />
        public List<int> StartedChannelNodes { get; }

        public ChannelService(ChannelSettings channelSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IChannelRepository channelRepository)
        {
            this.channelSettings = channelSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.StartedChannelNodes = new List<int>();
            this.channelRepository = channelRepository;
        }

        public async Task StartChannelNodeAsync(ChannelCreationRequest request)
        {
            try
            {
                this.logger.LogInformation($"Starting a node on channel '{request.Name}'.");

                Process process = await StartNodeAsync(request.Name, "-ischannelnode=true");
                if (process.HasExited)
                    this.logger.LogWarning($"Failed to start node on channel '{request.Name}' as the process exited early.");

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

        public void RestartChannelNodes()
        {
            try
            {
                this.channelRepository.GetChannelDefinitions().Keys.ToList().ForEach(async n => await this.RestartChannelNodeAsync(n));
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to restart channel nodes: {ex.Message}");
            }
        }

        public async Task RestartChannelNodeAsync(string channelName)
        {
            try
            {
                this.logger.LogInformation($"Restarting a node on channel '{channelName}'.");

                Process process = await RestartNodeAsync(channelName);
                if (process.HasExited)
                    this.logger.LogWarning($"Failed to restart node on channel '{channelName}' as the process exited early.");

                this.StartedChannelNodes.Add(process.Id);

                this.logger.LogInformation($"Node restarted on channel '{channelName}'.");

            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to restart node on channel '{channelName}': {ex.Message}");
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
            // Write teh serialized version of the network to disk.
            ChannelNetwork channelNetwork = WriteChannelNetworkJson(channelName);

            // Copy the parent node's authority and client certificate to the channel node's root.
            CopyCertificatesToChannelRoot(channelNetwork);

            // Create channel configuration file.
            CreateChannelConfigurationFile(channelNetwork, channelArgs);

            // Pass the path to the serialized network to the system channel node and start it.
            return await StartTheProcessAsync(channelNetwork);
        }

        private async Task<Process> RestartNodeAsync(string channelName)
        {
            // Write the serialized version of the network to disk.
            ChannelNetwork channelNetwork = WriteChannelNetworkJson(channelName);

            // Copy the parent node's authority and client certificate to the channel node's root.
            CopyCertificatesToChannelRoot(channelNetwork);

            // Pass the path to the serialized network to the system channel node and start it.
            return await StartTheProcessAsync(channelNetwork);
        }

        /// <summary> Write the serialized network to disk. </summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <returns>An instance of the <see cref="ChannelNetwork"/>.</returns>
        private ChannelNetwork WriteChannelNetworkJson(string channelName)
        {
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, $"{this.nodeSettings.DataFolder.RootPath}\\channels\\{channelName.ToLowerInvariant()}");
            var serializedJson = JsonSerializer.Serialize(channelNetwork);
            Directory.CreateDirectory(channelNetwork.RootFolderName);
            var filePath = $"{channelNetwork.RootFolderName}\\{channelName}_network.json";
            File.WriteAllText(filePath, serializedJson);
            return channelNetwork;
        }

        private void CopyCertificatesToChannelRoot(ChannelNetwork channelNetwork)
        {
            File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.AuthorityCertificateName), Path.Combine(channelNetwork.RootFolderName, CertificatesManager.AuthorityCertificateName));
            File.Copy(Path.Combine(this.nodeSettings.DataDir, CertificatesManager.ClientCertificateName), Path.Combine(channelNetwork.RootFolderName, CertificatesManager.ClientCertificateName));
        }

        private void CreateChannelConfigurationFile(ChannelNetwork channelNetwork, params string[] channelArgs)
        {
            var args = new StringBuilder();
            args.AppendLine($"-apiport={this.channelSettings.ChannelApiPort}");
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

            File.WriteAllText(Path.Combine(channelNetwork.RootFolderName, ChannelConfigurationFileName), args.ToString());
        }

        private async Task<Process> StartTheProcessAsync(ChannelNetwork channelNetwork)
        {
            var process = new Process();
            process.StartInfo.WorkingDirectory = this.channelSettings.ProcessPath;
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"run --no-build -conf={ChannelConfigurationFileName} -datadir={channelNetwork.RootFolderName}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            this.logger.LogInformation("Executing a delay to wait for the node to start.");
            await Task.Delay(TimeSpan.FromSeconds(10));

            return process;
        }

        public void StopChannelNodes()
        {
            foreach (var channelNodePId in this.StartedChannelNodes)
            {
                var process = Process.GetProcessById(channelNodePId);

                this.logger.LogInformation($"Stopping channel node with PId: {channelNodePId}.");

                // Wait for main window to be created, if not created already.
                process.WaitForInputIdle();

                // Close the main window.
                process.CloseMainWindow();

                // Wait until the process exits.
                process.WaitForExit();

                this.logger.LogInformation($"Channel node stopped.");
            }
        }
    }
}
