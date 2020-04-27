using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelAddMemberRequest
    {
        /// <summary> The organisation of the member.</summary>
        [JsonPropertyName("organisation")]
        public string Organisation { get; set; }

        /// <summary> The name of the channel.</summary>
        [JsonPropertyName("channel")]
        public string ChannelName { get; set; }

        [JsonPropertyName("member")]
        public string PubKeyHex { get; set; }
    }
}
