using System.Text.Json;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class TokenlessNetworkTests
    {
        [Fact]
        public void CanSerializeAndDeserializeNetwork()
        {
            var channelNetwork = TokenlessNetwork.CreateChannelNetwork("Test", "TestFolder");

            var serialized = JsonSerializer.Serialize()
        }
    }
}
