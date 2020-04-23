using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelAddMemberRequest
    {
        /// <summary> The organisation of the member.</summary>
        [JsonPropertyName("organisation")]
        public string Organisation { get; set; }

        /// <summary> The name of the channel.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("member")]
        public string PubKey { get; set; }
    }
}
