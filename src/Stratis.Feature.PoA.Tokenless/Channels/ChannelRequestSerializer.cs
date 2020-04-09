using System;
using System.Text;
using System.Text.Json;
using NBitcoin;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelRequestSerializer
    {
        /// <summary> Deserializes raw bytes to channel request object.</summary>
        (T, string) Deserialize<T>(Script script);

        /// <summary> Serializes a channel request object to raw bytes.</summary>
        byte[] Serialize<T>(T request);
    }

    public sealed class ChannelRequestSerializer : IChannelRequestSerializer
    {
        public const int OpcodeSize = sizeof(byte);

        /// <inheritdoc/>
        public byte[] Serialize<T>(T request)
        {
            var requestJson = JsonSerializer.Serialize(request);
            var requestJsonBytes = Encoding.Unicode.GetBytes(requestJson);

            var requestBytes = new byte[OpcodeSize + requestJsonBytes.Length];

            requestBytes[0] = (byte)ChannelOpCodes.OP_CREATECHANNEL;
            requestJsonBytes.CopyTo(requestBytes, OpcodeSize);

            return requestBytes;
        }

        /// <inheritdoc/>
        public (T, string) Deserialize<T>(Script script)
        {
            try
            {
                var bytes = script.ToBytes();

                if (bytes[0] != (byte)ChannelOpCodes.OP_CREATECHANNEL)
                    return default;

                var channelRequestBytes = bytes.Slice(OpcodeSize, (uint)(bytes.Length - OpcodeSize));
                var jsonString = Encoding.Unicode.GetString(channelRequestBytes);
                T request = JsonSerializer.Deserialize<T>(jsonString);
                return (request, null);
            }
            catch (Exception ex)
            {
                return (default, ex.Message);
            }
        }
    }
}