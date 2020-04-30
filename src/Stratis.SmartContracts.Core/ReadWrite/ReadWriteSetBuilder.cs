﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public interface IReadWriteSetOperations
    {
        void AddReadItem(ReadWriteSetKey key, string version);
        void AddWriteItem(ReadWriteSetKey key, byte[] value, bool isPrivateData = false);

        bool GetWriteItem(ReadWriteSetKey key, out byte[] value);
    }

    /// <summary>
    /// Constructs a ReadWriteSet with the logic for when to record reads and writes.
    /// </summary>
    public class ReadWriteSetBuilder : IReadWriteSetOperations
    {
        /// <summary>
        /// Key: {storageKey}, Value: {version}.
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, string> readSet;

        /// <summary>
        /// Key: {storageKey}, Value: {writtenBytes}, {isPrivateData}
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, (byte[], bool)> writeSet;

        public IReadOnlyDictionary<ReadWriteSetKey, string> ReadSet => this.readSet;

        public IReadOnlyDictionary<ReadWriteSetKey, (byte[] Bytes, bool IsPrivateData)> WriteSet => this.writeSet;

        public ReadWriteSetBuilder()
        {
            this.readSet = new Dictionary<ReadWriteSetKey, string>();
            this.writeSet = new Dictionary<ReadWriteSetKey, (byte[], bool)>();
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

        public void AddWriteItem(ReadWriteSetKey key, byte[] value, bool isPrivateData = false)
        {
            // Always store the last value for every key. We clone to avoid issues where the byte array might be altered afterwards.
            byte[] clonedValue = new byte[value.Length];
            Array.Copy(value, clonedValue, value.Length);
            this.writeSet[key] = (clonedValue, isPrivateData);
        }

        public bool GetWriteItem(ReadWriteSetKey key, out byte[] value)
        {
            var keyExists = this.writeSet.ContainsKey(key);

            value = keyExists ? this.writeSet[key].Item1 : null;

            return keyExists;
        }

        public void Merge(ReadWriteSetBuilder toMerge)
        {
            MergeReadSet(toMerge);
            MergeWriteSet(toMerge);
        }

        public void MergeReadSet(ReadWriteSetBuilder toMerge)
        {
            foreach (KeyValuePair<ReadWriteSetKey, string> read in toMerge.ReadSet.ToList())
            {
                this.AddReadItem(read.Key, read.Value);
            }
        }

        public void MergeWriteSet(ReadWriteSetBuilder toMerge)
        {
            foreach (KeyValuePair<ReadWriteSetKey, (byte[] Bytes, bool IsPrivateData)> write in toMerge.WriteSet.ToList())
            {
                this.AddWriteItem(write.Key, write.Value.Bytes);
            }
        }

        public ReadWriteSet GetReadWriteSet()
        {
            return ReadWriteSet.FromBuilder(this);
        }
    }
}
