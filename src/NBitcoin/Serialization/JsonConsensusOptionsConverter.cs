using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin.PoA;

namespace NBitcoin.Serialization
{
    public sealed class JsonConsensusOptionsConverter : JsonConverter<ConsensusOptions>
    {
        public override ConsensusOptions Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jsonString = reader.GetString();

            ConsensusOptions result;

            if (typeToConvert.IsAssignableFrom(typeof(PoAConsensusOptions)))
                result = JsonSerializer.Deserialize<PoAConsensusOptions>(jsonString);
            else
                result = JsonSerializer.Deserialize<ConsensusOptions>(jsonString);

            return result;
        }

        public override void Write(Utf8JsonWriter writer, ConsensusOptions value, JsonSerializerOptions options)
        {
            if (value is PoAConsensusOptions poAConsensusOptions)
            {
                writer.WriteStringValue(JsonSerializer.Serialize(poAConsensusOptions));
                return;
            }

            writer.WriteStringValue(JsonSerializer.Serialize(value));
        }
    }
}
