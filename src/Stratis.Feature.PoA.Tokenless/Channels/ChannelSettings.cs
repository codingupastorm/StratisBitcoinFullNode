using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Core.Configuration;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelSettings
    {
        public readonly string ChannelParentPipeName;
        public readonly string ChannelName;
        public readonly string InfraNodeApiUri;
        public readonly int InfraNodeApiPort;
        public readonly bool IsChannelNode;
        public readonly bool IsInfraNode;
        public readonly bool IsSystemChannelNode;
        public readonly string ProcessPath;

        /// <summary>Will attempt to start the node from source else start the node from an executable dll.</summary>
        public readonly bool ProjectMode;

        /// <summary>Bind the system channel api to different port.</summary>
        public readonly int SystemChannelApiPort;

        /// <summary>Bind the system channel api to different address.</summary>
        public readonly string SystemChannelApiUri;

        /// <summary>Bind the system channel to listen on a different port.</summary>
        public readonly int SystemChannelProtocolPort;

        /// <summary>Bind the system channel to listen on a different address.</summary>
        public readonly string SystemChannelProtocolUri;

        /// <summary>List of all system channel nodes known to the network.</summary>
        public readonly HashSet<IPEndPoint> SystemChannelNodeAddresses;

        public ChannelSettings()
        {
            this.SystemChannelNodeAddresses = new HashSet<IPEndPoint>();
        }

        public ChannelSettings(NodeSettings nodeSettings)
        {
            this.ChannelParentPipeName = nodeSettings.ConfigReader.GetOrDefault("channelparentpipename", (string)null);
            this.ChannelName = nodeSettings.ConfigReader.GetOrDefault("channelname", "");
            this.InfraNodeApiUri = nodeSettings.ConfigReader.GetOrDefault("infranodeapiuri", (string)null);
            this.InfraNodeApiPort = nodeSettings.ConfigReader.GetOrDefault("infranodeapiport", 0);
            this.IsChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = nodeSettings.ConfigReader.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = nodeSettings.ConfigReader.GetOrDefault("channelprocesspath", "");
            this.ProjectMode = nodeSettings.ConfigReader.GetOrDefault("projectmode", false);
            this.SystemChannelApiUri = nodeSettings.ConfigReader.GetOrDefault("systemchannelapiuri", (string)null);
            this.SystemChannelApiPort = nodeSettings.ConfigReader.GetOrDefault("systemchannelapiport", 0);
            this.SystemChannelProtocolUri = nodeSettings.ConfigReader.GetOrDefault("systemchannelprotocoluri", (string)null);
            this.SystemChannelProtocolPort = nodeSettings.ConfigReader.GetOrDefault("systemchannelprotocolport", 0);

            AddSystemChannelNodes(nodeSettings.ConfigReader);
        }

        public ChannelSettings(TextFileConfiguration fileConfiguration)
        {
            this.ChannelParentPipeName = fileConfiguration.GetOrDefault("channelparentpipename", (string)null);
            this.ChannelName = fileConfiguration.GetOrDefault("channelname", "");
            this.ChannelParentPipeName = fileConfiguration.GetOrDefault("channelparentpipename", (string)null);
            this.ChannelParentPipeName = fileConfiguration.GetOrDefault("channelparentpipename", (string)null);
            this.InfraNodeApiUri = fileConfiguration.GetOrDefault("infranodeapiuri", (string)null);
            this.InfraNodeApiPort = fileConfiguration.GetOrDefault("infranodeapiport", 0);
            this.IsChannelNode = fileConfiguration.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = fileConfiguration.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = fileConfiguration.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = fileConfiguration.GetOrDefault("channelprocesspath", "");
            this.ProjectMode = fileConfiguration.GetOrDefault("projectmode", false);
            this.SystemChannelApiUri = fileConfiguration.GetOrDefault("systemchannelapiuri", (string)null);
            this.SystemChannelProtocolUri = fileConfiguration.GetOrDefault("systemchannelprotocoluri", (string)null);
            this.SystemChannelProtocolPort = fileConfiguration.GetOrDefault("systemchannelprotocolport", 0);

            AddSystemChannelNodes(fileConfiguration);
        }

        private void AddSystemChannelNodes(TextFileConfiguration configuration)
        {
            foreach (IPEndPoint systemChannelNode in configuration.GetAll("systemchannelnode").Select(c => IPEndPoint.Parse(c)))
                this.SystemChannelNodeAddresses.Add(systemChannelNode);
        }
    }
}