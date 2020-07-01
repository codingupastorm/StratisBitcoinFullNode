using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Stratis.Feature.PoA.Tokenless.Models
{
    public sealed class SystemChannelAddressesModel
    {
        public SystemChannelAddressesModel()
        {
            this.Addresses = new List<string>();
        }

        /// <summary>
        /// A list of known system channel nodes on the network.
        /// </summary>
        [JsonPropertyName("addresses")]
        public List<string> Addresses { get; set; }
    }
}
