using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    }

    /// <inheritdoc />
    public sealed class ChannelService : IChannelService
    {
        private const string SystemChannelName = "system";

        private readonly ChannelSettings channelSettings;
        private readonly ILogger logger;
        private readonly NodeSettings nodeSettings;

        /// <inheritdoc />
        public List<int> StartedChannelNodes { get; }

        public ChannelService(ChannelSettings channelSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings)
        {
            this.channelSettings = channelSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.StartedChannelNodes = new List<int>();
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

                this.logger.LogInformation($"Node started on channel '{request.Name}'.");
            }
            catch (Exception ex)
            {
                throw new ChannelServiceException($"Failed to start node on channel '{request.Name}': {ex.Message}");
            }
        }

        public async Task StartSystemChannelNodeAsync()
        {
            try
            {
                this.logger.LogInformation("Starting a system channel node.");

                Process process = await StartNodeAsync(SystemChannelName, "-issystemchannelnode=true -ischannelnode=true");
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

        private async Task<Process> StartNodeAsync(string channelName, string channelArgs)
        {
            // Write the serialized network to disk.
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
            process.StartInfo.WorkingDirectory = this.channelSettings.ProcessPath;
            process.StartInfo.FileName = "dotnet";

            var args = new StringBuilder();
            args.Append($"-apiport={this.channelSettings.ChannelApiPort} ");
            args.Append("-certificatepassword=test ");
            args.Append("-password=test ");
            args.Append("-conf=poa.conf ");
            args.Append($"-datadir={channelNetwork.RootFolderName} ");
            args.Append($"-{CertificatesManager.CaAccountIdKey}={Settings.AdminAccountId} ");
            args.Append($"-{CertificatesManager.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificatesManager.CaPasswordKey, "")} ");
            args.Append($"-{CertificatesManager.ClientCertificateConfigurationKey}=test ");

            // Apend any channel specific arguments.
            args.Append(channelArgs);

            process.StartInfo.Arguments = $"run --no-build {args.ToString()}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();

            this.logger.LogInformation("Executing delay to wait for node to start.");
            await Task.Delay(TimeSpan.FromSeconds(5));

            return process;
        }

        public void StopChannelNodes()
        {
            foreach (var channelNodePId in this.StartedChannelNodes)
            {
                var process = Process.GetProcessById(channelNodePId);

                this.logger.LogInformation($"Stopping channel node with PId: {channelNodePId}.");
                // TODO-TL: Need to gracefully shutdown
                process.Kill();
                //process.CloseMainWindow();
                //process.WaitForExit();
                this.logger.LogInformation($"Channel node stopped.");
            }
        }
    }
}
