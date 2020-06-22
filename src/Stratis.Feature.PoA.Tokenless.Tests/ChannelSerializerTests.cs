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
                Identifier = "test",
                Name = "test"
            };

            var serializer = new ChannelRequestSerializer();
            byte[] serialized = serializer.Serialize(request);
            Assert.Equal(serialized[0], (byte)ChannelOpCodes.OP_CREATECHANNEL);
            Assert.True(serialized.Length > 1);

            var script = new Script(serialized);
            (ChannelCreationRequest deserialized, _) = serializer.Deserialize<ChannelCreationRequest>(script);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.Name);
        }

        [Fact]
        public void CanReturnDeserializationError()
        {
            var requestBytes = new byte[sizeof(byte) + 1];
            requestBytes[0] = (byte)ChannelOpCodes.OP_CREATECHANNEL;
            requestBytes[1] = 0;

            var script = new Script(requestBytes);
            var serializer = new ChannelRequestSerializer();
            (ChannelCreationRequest deserialized, string message) = serializer.Deserialize<ChannelCreationRequest>(script);
            Assert.Null(deserialized);
            Assert.NotNull(message);
        }
    }
}
