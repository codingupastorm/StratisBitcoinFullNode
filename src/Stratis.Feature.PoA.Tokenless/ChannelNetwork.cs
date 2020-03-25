using System.Text.Json.Serialization;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Core.Serialization;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// Serializable version of the <see cref="Network"/> class.
    /// </summary>
    public sealed class ChannelNetwork : Network
    {
        [JsonPropertyName("genesisblock")]
        [JsonConverter(typeof(TokenlessGenesisBlockConverter))]
        public override Block Genesis { get; set; }
    }
}
