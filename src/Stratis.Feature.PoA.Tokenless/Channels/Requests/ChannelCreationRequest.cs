using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelCreationRequest
    {
        /// <summary> The organisation to add to the channel.</summary>
        [JsonPropertyName("organisation")]
        [Required]
        public string Organisation { get; set; }

        /// <summary> The name of the channel to create.</summary>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; }
    }
}
