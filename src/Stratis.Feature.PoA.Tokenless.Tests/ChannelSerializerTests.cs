using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class ChannelSerializerTests
    {
        [Fact]
        public void CanSerializeAndDeserializeChannelRequest()
        {
            var request = new ChannelCreationRequest()
            {
                Name = "test"
            };

            var serializer = new ChannelRequestSerializer();
            byte[] serialized = serializer.Serialize(request);
            Assert.Equal(serialized[0], (byte)ChannelOpCodes.OP_CREATECHANNEL);
            Assert.True(serialized.Length > 1);

            var script = new Script(serialized);
            ChannelCreationRequest deserialized = serializer.Deserialize<ChannelCreationRequest>(script);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.Name);
        }
    }
}
