using System.Text.Json;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class TokenlessNetworkTests
    {
        [Fact]
        public void CanSerializeAndDeserializeNetwork()
        {
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork("Test", "TestFolder");

            var serialized = JsonSerializer.Serialize(channelNetwork);
            ChannelNetwork deserialized = JsonSerializer.Deserialize<ChannelNetwork>(serialized);

            Assert.NotNull(deserialized.Consensus);
            Assert.NotNull(deserialized.Consensus.Options);

            Assert.Equal(channelNetwork.Consensus.MaxReorgLength, deserialized.Consensus.MaxReorgLength);

            Assert.Equal(channelNetwork.DefaultAPIPort, deserialized.DefaultAPIPort);
            Assert.Equal(channelNetwork.DefaultBanTimeSeconds, deserialized.DefaultBanTimeSeconds);
            Assert.Equal(channelNetwork.DefaultConfigFilename, deserialized.DefaultConfigFilename);
            Assert.Equal(channelNetwork.DefaultEnableIpRangeFiltering, deserialized.DefaultEnableIpRangeFiltering);
            Assert.Equal(channelNetwork.DefaultMaxInboundConnections, deserialized.DefaultMaxInboundConnections);
            Assert.Equal(channelNetwork.DefaultMaxOutboundConnections, deserialized.DefaultMaxOutboundConnections);
            Assert.Equal(channelNetwork.DefaultPort, deserialized.DefaultPort);
            Assert.Equal(channelNetwork.DefaultSignalRPort, deserialized.DefaultSignalRPort);
        }
    }
}
