using System;
using System.Linq;

namespace Stratis.SmartContracts.Core.State
{
    public class StorageValue
    {
        /// <summary>
        /// We will be able to use this to find all instances where we need to insert the version.
        /// </summary>
        public const int InsertVersion = 0;

        public byte[] Value { get;}

        // TODO: HL uses a blockNumber-txNumber tuple for the version.
        public uint Version { get; }

        public StorageValue(byte[] value, uint version)
        {
            this.Value = value;
            this.Version = version;
        }


        public static StorageValue FromBytes(byte[] serialized)
        {
            byte[] versionBytes = serialized.Take(sizeof(uint)).ToArray();
            uint version = BitConverter.ToUInt32(versionBytes);
            byte[] valueBytes = serialized.Skip(sizeof(uint)).ToArray();
            return new StorageValue(valueBytes, version);
        }

        public byte[] ToBytes()
        {
            byte[] versionBytes = BitConverter.GetBytes(this.Version);
            return versionBytes.Concat(this.Value).ToArray();
        }
    }
}
