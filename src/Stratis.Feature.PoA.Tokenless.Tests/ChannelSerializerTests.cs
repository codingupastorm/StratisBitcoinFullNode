using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
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
                Name = "test",
                Endorsements = new List<Endorsement.Endorsement>()
                {
                    new Endorsement.Endorsement(new byte[] { 0xAA }, new byte[] { 0xBB }),
                    new Endorsement.Endorsement(new byte[] { 0xCC }, new byte[] { 0xD })
                }
            };

            var serializer = new ChannelRequestSerializer();
            byte[] serialized = serializer.Serialize(request);
            Assert.Equal(serialized[0], (byte)ChannelOpCodes.OP_CREATECHANNEL);
            Assert.True(serialized.Length > 1);

            var script = new Script(serialized);
            (ChannelCreationRequest deserialized, _) = serializer.Deserialize<ChannelCreationRequest>(script);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.Name);

            // Round trip
            Assert.True(new ByteArrayComparer().Equals(serialized, serializer.Serialize(deserialized)));
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

        [Fact]
        public void CanSerializeAndDeserializeChannelAddMemberRequest()
        {
            var request = new ChannelAddMemberRequest()
            {
                ChannelName = "test",
                Organisation = "org",
                PubKeyHex = "123"
            };

            var serializer = new ChannelRequestSerializer();
            byte[] serialized = serializer.Serialize(request);
            Assert.Equal(serialized[0], (byte)ChannelOpCodes.OP_ADDCHANNELMEMBER);
            Assert.True(serialized.Length > 1);

            var script = new Script(serialized);
            (ChannelAddMemberRequest deserialized, _) = serializer.Deserialize<ChannelAddMemberRequest>(script);
            Assert.NotNull(deserialized);
            Assert.Equal("test", deserialized.ChannelName);
            Assert.Equal("org", deserialized.Organisation);
            Assert.Equal("123", deserialized.PubKeyHex);
        }
    }
}
