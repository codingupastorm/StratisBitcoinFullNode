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
        public ChannelNetwork()
        {
            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (55) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (63 + 128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
        }

        [JsonPropertyName("genesisblock")]
        [JsonConverter(typeof(TokenlessGenesisBlockConverter))]
        public override Block Genesis { get; set; }
    }
}
