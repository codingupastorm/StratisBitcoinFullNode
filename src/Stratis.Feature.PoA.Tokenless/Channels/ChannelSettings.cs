using Stratis.Bitcoin.Configuration;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelSettings
    {
        public readonly string ChannelName;
        public readonly bool IsChannelNode;
        public readonly bool IsInfraNode;
        public readonly bool IsSystemChannelNode;
        public readonly string ProcessPath;

        public ChannelSettings(NodeSettings nodeSettings)
        {
            this.ChannelName = nodeSettings.ConfigReader.GetOrDefault("channelname", "");
            this.IsChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = nodeSettings.ConfigReader.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = nodeSettings.ConfigReader.GetOrDefault("channelprocesspath", "");
        }

        public ChannelSettings(TextFileConfiguration fileConfiguration)
        {
            this.ChannelName = fileConfiguration.GetOrDefault("channelname", "");
            this.IsChannelNode = fileConfiguration.GetOrDefault<bool>("ischannelnode", false);
            this.IsInfraNode = fileConfiguration.GetOrDefault<bool>("isinfranode", false);
            this.IsSystemChannelNode = fileConfiguration.GetOrDefault<bool>("issystemchannelnode", false);
            this.ProcessPath = fileConfiguration.GetOrDefault("channelprocesspath", "");
        }
    }
}
