using System.Text;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Core.State
{
    public class StorageValue
    {
        /// <summary>
        /// We will be able to use this to find all instances where we need to insert the version.
        /// </summary>
        public const string InsertVersion = "0.0";

        public static StorageValue Default = new StorageValue(null, InsertVersion);

        public byte[] Value { get;}

        /// <summary>
        /// A version of the format {blockNumber}.{txNumber}
        /// </summary>
        public string Version { get; }

        public StorageValue(byte[] value, string version)
        {
            this.Value = value;
            this.Version = version;
        }

        public static StorageValue FromBytes(byte[] serialized)
        {
            // Using RLP and a string encoding for the version allows maximum flexibility in case we change the versioning schema.
            // When going full raft we can just store this struct without doing this encoding.
            var collection = (RLPCollection) RLP.Decode(serialized)[0];
            byte[] versionBytes = collection[0].RLPData;
            string version = Encoding.UTF8.GetString(versionBytes);
            byte[] valueBytes = collection[1].RLPData;
            return new StorageValue(valueBytes, version);
        }

        public byte[] ToBytes()
        {
            byte[] versionBytes = Encoding.UTF8.GetBytes(this.Version);
            return RLP.EncodeList(RLP.EncodeElement(versionBytes), RLP.EncodeElement(this.Value));
        }
    }
}
