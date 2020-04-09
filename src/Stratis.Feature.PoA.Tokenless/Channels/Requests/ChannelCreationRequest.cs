using System.Text.Json.Serialization;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelCreationRequest : IBitcoinSerializable
    {
        /// <summary> The organisation to add to the channel.</summary>
        [JsonPropertyName("organisation")]
        public string Organisation { get; set; }

        /// <summary> The name of the channel to create.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        public void ReadWrite(BitcoinStream s)
        {
            string org = this.Organisation;
            s.ReadWrite(ref org);

            string name = this.Name;
            s.ReadWrite(ref name);

            if (!s.Serializing)
            {
                this.Name = name;
                this.Organisation = org;
            }
        }
    }
}
