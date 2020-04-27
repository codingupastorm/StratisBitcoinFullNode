using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NBitcoin.Serialization
{
    public sealed class JsonListInterfaceConverter<TConcrete, TInterface> : JsonConverter<List<TInterface>> where TConcrete : TInterface
    {
        public override List<TInterface> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            List<TConcrete> result = JsonSerializer.Deserialize<List<TConcrete>>(reader.GetString());
            var interfaceList = result.Select(r => (TInterface)r).ToList();
            return interfaceList;
        }

        public override void Write(Utf8JsonWriter writer, List<TInterface> value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(JsonSerializer.Serialize(value));
        }
    }
}
