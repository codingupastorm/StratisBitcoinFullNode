using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Stratis.SmartContracts.Core.AccessControl;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelCreationRequest
    {
        /// <summary> The organisations to add to the channel.</summary>
        [JsonPropertyName("accessList")]
        [Required]
        public AccessControlList AccessList { get; set; }

        /// <summary> A unique 4 character identifier which will be used to set the newly created network's <see cref="NBitcoin.Network.MagicBytes"/>.</summary>
        [JsonPropertyName("identifier")]
        [Required]
        public string Identifier { get; set; }

        /// <summary> The name of the channel to create.</summary>
        [JsonPropertyName("name")]
        [Required]
        public string Name { get; set; }

        public (bool isValid, string message) IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(this.Identifier))
                    return (false, $"'{nameof(this.Identifier)}' is null.");

                if (this.Identifier.Length != 4)
                    return (false, $"'{nameof(this.Identifier)}'s length must be 4 characters.");

                if (string.IsNullOrEmpty(this.Name))
                    return (false, $"'{nameof(this.Name)}' is null.");

                return (true, null);
            }
        }
    }
}
