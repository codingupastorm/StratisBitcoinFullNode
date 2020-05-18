using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using Stratis.Core.AsyncWork;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ProcessChannelService : ChannelService
    {
        public const int SystemChannelId = 1;
        private const string SystemChannelName = "system";

        private const string ChannelConfigurationFileName = "channel.conf";

        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;

        /// <inheritdoc />
        public List<ChannelNodeProcess> StartedChannelNodes { get; }

        public ProcessChannelService(ChannelSettings channelSettings, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory, NodeSettings nodeSettings, IChannelRepository channelRepository, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider) 
            : base(channelSettings, dateTimeProvider, loggerFactory, nodeSettings, channelRepository)
        {
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            this.StartedChannelNodes = new List<ChannelNodeProcess>();
        }

        public override void Initialize()
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

        private void CreateChannelConfigurationFile(string channelRootFolder, params string[] channelArgs)
        {
            var configurationFilePath = Path.Combine(channelRootFolder, ChannelConfigurationFileName);

            var args = new StringBuilder();
            args.AppendLine($"-certificatepassword=test");
            args.AppendLine($"-password=test");
            args.AppendLine($"-{CertificateAuthorityInterface.CaBaseUrlKey}={CertificateAuthorityInterface.CaBaseUrl}");
            args.AppendLine($"-{CertificateAuthorityInterface.CaAccountIdKey}={Settings.AdminAccountId}");
            args.AppendLine($"-{CertificateAuthorityInterface.CaPasswordKey}={this.nodeSettings.ConfigReader.GetOrDefault(CertificateAuthorityInterface.CaPasswordKey, "")} ");
            args.AppendLine($"-{CertificateAuthorityInterface.ClientCertificateConfigurationKey}=test");
            args.AppendLine($"-agent{CertificateAuthorityInterface.ClientCertificateConfigurationKey}=test");

            // Append any channel specific arguments.
            foreach (var channelArg in channelArgs)
            {
                args.AppendLine(channelArg);
            }

            File.WriteAllText(configurationFilePath, args.ToString());
        }

        protected override async Task<bool> StartChannelAsync(string channelRootFolder, params string[] channelArgs)
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

            // TODO: The code always adds the node to the list AND logs that the process was started, even if it exited. Should that be changed?
            lock (this.StartedChannelNodes)
                this.StartedChannelNodes.Add(channelNodeProcess);

            // TODO: Log channel name here?
            this.logger.LogInformation($"Node started with Pid '{process.Id}'.");

            return !process.HasExited;
        }

        public override void StopChannelNodes()
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
