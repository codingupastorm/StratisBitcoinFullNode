using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelCreationRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
