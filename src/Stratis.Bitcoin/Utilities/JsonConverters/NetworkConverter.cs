//using System;
//using NBitcoin;
//using Newtonsoft.Json;

//namespace Stratis.Bitcoin.Utilities.JsonConverters
//{
//    /// <summary>
//    /// Converter used to convert <see cref="Network"/> to and from JSON.
//    /// </summary>
//    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
//    public class NetworkConverter : JsonConverter
//    {
//        private readonly Network network;

//        public NetworkConverter(Network network)
//        {
//            this.network = network;
//        }

//        /// <inheritdoc />
//        public override bool CanConvert(Type objectType)
//        {
//            return objectType == typeof(Network);
//        }

//        /// <inheritdoc />
//        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//        {
//            string value = (string) reader.Value;

//            // Ensure the node reading this Network name is on the correct Network.
//            Guard.Assert(value == this.network.ToString());

//            return this.network;
//        }

//        /// <inheritdoc />
//        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
//        {
//            writer.WriteValue(((Network)value).ToString());
//        }
//    }
//}
