using System.Text.Json.Serialization;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// Serializable version of the <see cref="Network"/> class.
    /// </summary>
    public sealed class ChannelNetwork : Network
    {
        [JsonPropertyName("genesishex")]
        public string GenesisHex { get; set; }
    }
}
