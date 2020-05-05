using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Core.Serialization;
using Stratis.Feature.PoA.Tokenless.Mempool;

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

        /// <summary>
        /// For our MVP, the Organisation acts as an access list for the network. Everyone in an organisation can access the channel.
        /// </summary>
        [JsonPropertyName("organisation")]
        public string Organisation { get; set; }

        /// <summary>
        /// Deserializes the given json string to a new instance of <see cref="ChannelNetwork"/>.
        /// </summary>
        /// <param name="channelSettings">The string containing the Json.</param>
        /// <returns>A new instance of <see cref="ChannelNetwork"/>.</returns>
        public static ChannelNetwork Construct(string channelDataFolder, string channelName, bool isSystemChannelNode)
        {
            var json = File.ReadAllText($"{channelDataFolder}\\{channelName}_network.json");

            ChannelNetwork channelNetwork = JsonSerializer.Deserialize<ChannelNetwork>(json);
            channelNetwork.Consensus.ConsensusFactory = new TokenlessConsensusFactory();
            channelNetwork.Consensus.HashGenesisBlock = channelNetwork.Genesis.GetHash();

            if (isSystemChannelNode)
            {
                TokenlessConsensusRuleSet.CreateForSystemChannel(channelNetwork);
                TokenlessMempoolRuleSet.CreateForSystemChannel(channelNetwork);
            }
            else
            {
                TokenlessConsensusRuleSet.Create(channelNetwork);
                TokenlessMempoolRuleSet.Create(channelNetwork);
            }

            return channelNetwork;
        }
    }
}
