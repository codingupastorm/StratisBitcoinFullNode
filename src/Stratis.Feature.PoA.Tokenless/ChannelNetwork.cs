using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Core.Serialization;
using Stratis.Features.PoA;

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

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();
        }

        [JsonPropertyName("genesisblock")]
        [JsonConverter(typeof(TokenlessGenesisBlockConverter))]
        public override Block Genesis { get; set; }

        /// <summary>
        /// Deserialized the given json string to a new instance of <see cref="ChannelNetwork"/>.
        /// </summary>
        /// <param name="channelSettings">The string containing the Json.</param>
        /// <returns>A new instance of <see cref="ChannelNetwork"/>.</returns>
        public static ChannelNetwork Construct(ChannelSettings channelSettings, string channelDataFolder)
        {
            var json = File.ReadAllText($"{channelDataFolder}\\{channelSettings.ChannelName}_network.json");

            ChannelNetwork channelNetwork = JsonSerializer.Deserialize<ChannelNetwork>(json);
            channelNetwork.Consensus.ConsensusFactory = new TokenlessConsensusFactory();

            // TODO-TL: Add specific consensus rules here.
            channelNetwork.Consensus.ConsensusRules = new ConsensusRules();

            channelNetwork.Consensus.HashGenesisBlock = channelNetwork.Genesis.GetHash();
            channelNetwork.Consensus.Options = new PoAConsensusOptions(0, 0, 0, 0, 0, new List<IFederationMember>(), 16, false, false, false);

            // TODO-TL: Add specific mempool rules here.
            channelNetwork.Consensus.MempoolRules = new List<Type>();

            return channelNetwork;
        }
    }
}
