using System.Text;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Core.State
{
    public class StorageValue
    {
        /// <summary>
        /// The default version of a stored item. When a key does not exist, or an account state has
        /// not been initialized, this version will be returned along with the default storage value.
        /// </summary>
        public const string DefaultVersion = "0.0";

        public static StorageValue Default = new StorageValue(null, DefaultVersion);

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
