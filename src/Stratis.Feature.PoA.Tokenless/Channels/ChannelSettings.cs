using Stratis.Core.Configuration;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelSettings
    {
        public readonly string ChannelParentPipeName;
        public readonly string ChannelName;
        public readonly bool IsChannelNode;
        public readonly bool IsInfraNode;
        public readonly bool IsSystemChannelNode;
        public readonly string ProcessPath;

        /// <summary>Will attempt to start the node from source else start the node from an executable dll.</summary>
        public readonly bool ProjectMode;

        public readonly int SystemChannelApiPort;

        public ChannelSettings(NodeSettings nodeSettings)
        {
            this.ChannelParentPipeName = nodeSettings.ConfigReader.GetOrDefault("channelparentpipename", (string)null);
            this.ChannelName = nodeSettings.ConfigReader.GetOrDefault("channelname", "");
            this.IsChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = nodeSettings.ConfigReader.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = nodeSettings.ConfigReader.GetOrDefault("channelprocesspath", "");
            this.ProjectMode = nodeSettings.ConfigReader.GetOrDefault("projectmode", false);
            this.SystemChannelApiPort = nodeSettings.ConfigReader.GetOrDefault("systemchannelapiport", 0);
        }

        public ChannelSettings(TextFileConfiguration fileConfiguration)
        {
            this.ChannelParentPipeName = fileConfiguration.GetOrDefault("channelparentpipename", (string)null);
            this.ChannelName = fileConfiguration.GetOrDefault("channelname", "");
            this.IsChannelNode = fileConfiguration.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = fileConfiguration.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = fileConfiguration.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = fileConfiguration.GetOrDefault("channelprocesspath", "");
            this.ProjectMode = fileConfiguration.GetOrDefault("projectmode", false);
        }
    }
}
