using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelJoinRequest
    {
        /// <summary> The id of the channel to join.</summary>
        [JsonPropertyName("id")]
        [Required]
        public int Id { get; set; }

        /// <summary> The name of the channel to join.</summary>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; }
    }
}
