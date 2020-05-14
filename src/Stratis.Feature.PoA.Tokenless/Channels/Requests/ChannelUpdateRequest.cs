using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Stratis.Feature.PoA.Tokenless.AccessControl;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelUpdateRequest
    {
        /// <summary> The members to add to the channel.</summary>
        [JsonPropertyName("memberstoadd")]
        [Required]
        public AccessControlList MembersToAdd { get; set; }

        /// <summary> The members to remove from the channel.</summary>
        [JsonPropertyName("memberstoremove")]
        [Required]
        public AccessControlList MembersToRemove { get; set; }

        /// <summary> The name of the channel to update.</summary>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; }
    }
}
