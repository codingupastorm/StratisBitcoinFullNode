using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelSettings
    {
        private readonly ILogger logger;

        public readonly int ChannelApiPort;
        public readonly string ProcessPath;

        public ChannelSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            this.logger = nodeSettings.LoggerFactory.CreateLogger(this.GetType().FullName);

            this.ChannelApiPort = nodeSettings.ConfigReader.GetOrDefault("channelapiport", 0, this.logger);
            this.ProcessPath = nodeSettings.ConfigReader.GetOrDefault("channelprocesspath", "", this.logger);
        }
    }
}
