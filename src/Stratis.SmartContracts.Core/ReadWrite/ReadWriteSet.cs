using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    /// <summary>
    /// A DTO for the ReadWriteSet. Used to carry information around and go to and from JSON.
    /// </summary>
    public class ReadWriteSet
    {
        // TODO: Immutability would be nice but serialization take more configuration.

        public List<ReadItem> Reads { get; set; }

        public List<WriteItem> Writes { get; set; }

        /// <summary>
        /// Empty constructor required for serialization.
        /// </summary>
        public ReadWriteSet() { }

        public static ReadWriteSet FromBuilder(ReadWriteSetBuilder rws)
        {
            return new ReadWriteSet
            {
                Reads = rws.ReadSet.ToList().Select(x => new ReadItem
                {
                    ContractAddress = x.Key.ContractAddress,
                    Key = x.Key.Key,
                    Version = x.Value
                }).ToList(),

                Writes = rws.WriteSet.ToList().Select(x => new WriteItem
                {
                    ContractAddress = x.Key.ContractAddress,
                    Key = x.Key.Key,
                    Value = x.Value.Bytes,
                    IsPrivateData = x.Value.IsPrivateData
                }).ToList()
            };
        }

        // TODO: Don't be responsible for own serialization.

        public ReadWriteSet FromJson(string json)
        {
            return JsonConvert.DeserializeObject<ReadWriteSet>(json);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class ReadItem
    {
        [JsonConverter(typeof(Uint160HexConverter))]
        public uint160 ContractAddress { get; set; }

        [JsonConverter(typeof(ByteArrayHexConverter))]
        public byte[] Key { get; set; }

        public string Version { get; set; }
    }

    public class WriteItem
    {
        [JsonConverter(typeof(Uint160HexConverter))]
        public uint160 ContractAddress { get; set; }

        [JsonConverter(typeof(ByteArrayHexConverter))]
        public byte[] Key { get; set; }

        [JsonConverter(typeof(ByteArrayHexConverter))]
        public byte[] Value { get; set; }

        public bool IsPrivateData { get; set; }
    }
}
