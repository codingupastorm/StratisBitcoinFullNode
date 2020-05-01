using System;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public class ByteArrayHexConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(byte[]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var hex = serializer.Deserialize<string>(reader);
                if (!string.IsNullOrEmpty(hex))
                {
                    return hex.HexToByteArray();
                }
            }
            return Enumerable.Empty<byte>();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            byte[] arr = (byte[])value;
            serializer.Serialize(writer, arr.ToHexString());
        }
    }

    public class Uint160HexConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(uint160);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                var hex = serializer.Deserialize<string>(reader);
                return new uint160(hex);
            }
            return default(uint160);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            uint160 val = (uint160)value;
            serializer.Serialize(writer, val.ToString());
        }
    }
}
