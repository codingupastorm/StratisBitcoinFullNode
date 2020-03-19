using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NBitcoin
{
    public sealed class JsonInterfaceConverter<TConcrete, TInterface> : JsonConverter<TInterface> where TConcrete : class, TInterface
    {
        public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<TConcrete>(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(JsonSerializer.Serialize(value));
        }
    }
}
