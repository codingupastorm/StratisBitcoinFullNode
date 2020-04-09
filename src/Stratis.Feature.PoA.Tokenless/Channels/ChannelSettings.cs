using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelSettings
    {
        private readonly ILogger logger;

        public readonly int ChannelApiPort;
        public readonly bool IsChannelNode;
        public readonly bool IsInfraNode;
        public readonly bool IsSystemChannelNode;
        public readonly string ProcessPath;

        public ChannelSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);

            this.ChannelApiPort = nodeSettings.ConfigReader.GetOrDefault("channelapiport", 0, this.logger);
            this.IsChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("ischannelnode", false, this.logger);
            this.IsInfraNode = nodeSettings.ConfigReader.GetOrDefault<bool>("isinfranode", false, this.logger);
            this.IsSystemChannelNode = nodeSettings.ConfigReader.GetOrDefault<bool>("issystemchannelnode", false, this.logger);
            this.ProcessPath = nodeSettings.ConfigReader.GetOrDefault("channelprocesspath", "", this.logger);
        }
    }
}
