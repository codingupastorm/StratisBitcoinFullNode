using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public sealed class TokenlessGenesisBlockConverter : JsonConverter<Block>
    {
        public override Block Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var hex = reader.GetString();
            return Block.Parse(hex, new TokenlessConsensusFactory());
        }

        public override void Write(Utf8JsonWriter writer, Block value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToHex(new TokenlessConsensusFactory()));
        }
    }
}
