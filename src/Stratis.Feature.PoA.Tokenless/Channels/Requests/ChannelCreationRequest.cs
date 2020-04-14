using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelCreationRequest
    {
        /// <summary> The organisation to add to the channel.</summary>
        [JsonPropertyName("organisation")]
        public string Organisation { get; set; }

        /// <summary> The name of the channel to create.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
