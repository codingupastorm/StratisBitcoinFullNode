using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core
{
    public class ReadWriteSet
    {
        // TODO: Nested execution!

        // TODO: These types are a little dirty. Can we do something nicer than IReadOnlyCollection?

        /// <summary>
        /// Key: {storageKey}, Value: {version}.
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, string> readSet;

        /// <summary>
        /// Key: {storageKey}, Value: {writtenBytes}.
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, byte[]> writeSet;

        public IReadOnlyCollection<KeyValuePair<ReadWriteSetKey, string>> ReadSet => this.readSet;

        public IReadOnlyCollection<KeyValuePair<ReadWriteSetKey, byte[]>> WriteSet => this.writeSet;

        public ReadWriteSet()
        {
            this.readSet = new Dictionary<ReadWriteSetKey, string>();
            this.writeSet = new Dictionary<ReadWriteSetKey, byte[]>();
        }

        public void AddReadItem(ReadWriteSetKey key, string version)
        {
            // If an item is already in the list, don't add it again.
            if (this.readSet.ContainsKey(key))
                return;

            // If an item is already in the write set, don't add it. We would be reading that value, not the previous version's' value.
            if (this.writeSet.ContainsKey(key))
                return;

            this.readSet[key] = version;
        }

        public void AddWriteItem(ReadWriteSetKey key, byte[] value)
        {
            // Always store the last value for every key. We clone to avoid issues where the byte array might be altered afterwards.
            byte[] clonedValue = new byte[value.Length];
            Array.Copy(value, clonedValue, value.Length);
            this.writeSet[key] = clonedValue;
        }

        public void Merge(ReadWriteSet toMerge)
        {
            foreach (KeyValuePair<ReadWriteSetKey, string> read in toMerge.ReadSet.ToList())
            {
                this.AddReadItem(read.Key, read.Value);
            }

            foreach (KeyValuePair<ReadWriteSetKey, byte[]> write in toMerge.WriteSet.ToList())
            {
                this.AddWriteItem(write.Key, write.Value);
            }
        }
    }

    public struct ReadWriteSetKey
    {
        public uint160 ContractAddress { get; }
        public byte[] Key { get; }

        public ReadWriteSetKey(uint160 contractAddress, byte[] key)
        {
            this.ContractAddress = contractAddress;
            this.Key = key;
        }

        // TODO: These may be slow.

        public override bool Equals(object obj)
        {
            if (!(obj is ReadWriteSetKey))
                return false;

            ReadWriteSetKey other = (ReadWriteSetKey) obj;
            return Equals(this.ContractAddress, other.ContractAddress) && new ByteArrayComparer().Equals(this.Key, other.Key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.ContractAddress != null ? this.ContractAddress.GetHashCode() : 0) * 397) ^ (this.Key != null ? new BigInteger(this.Key).GetHashCode() : 0);
            }
        }
    }
}
