using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NBitcoin.Serialization
{
    public sealed class JsonPubKeyConverter : JsonConverter<PubKey>
    {
        public override PubKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var hex = JsonSerializer.Deserialize<string>(reader.GetString());
            return new PubKey(hex);
        }

        public override void Write(Utf8JsonWriter writer, PubKey value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(JsonSerializer.Serialize(value.ToHex()));
        }
    }
}
