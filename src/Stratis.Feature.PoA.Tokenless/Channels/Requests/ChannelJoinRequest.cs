using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelJoinRequest
    {
        /// <summary> The network json of the channel to join.</summary>
        [JsonPropertyName("network_json")]
        [Required]
        public string NetworkJson { get; set; }

        public int? Port { get; set; }

        public int? ApiPort { get; set; }

        public int? SignalRPort { get; set; }
    }
}
