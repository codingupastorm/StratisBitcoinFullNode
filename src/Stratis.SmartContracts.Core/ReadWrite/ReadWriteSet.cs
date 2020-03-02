using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public class ReadWriteSet
    {
        // TODO: These types are a little dirty. Can we do something nicer than IReadOnlyCollection?

        /// <summary>
        /// Key: {storageKey}, Value: {version}.
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, string> readSet;

        /// <summary>
        /// Key: {storageKey}, Value: {writtenBytes}.
        /// </summary>
        private readonly Dictionary<ReadWriteSetKey, byte[]> writeSet;

        public IReadOnlyDictionary<ReadWriteSetKey, string> ReadSet => this.readSet;

        public IReadOnlyDictionary<ReadWriteSetKey, byte[]> WriteSet => this.writeSet;

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
            MergeReadSet(toMerge);
            MergeWriteSet(toMerge);
        }

        public void MergeReadSet(ReadWriteSet toMerge)
        {
            foreach (KeyValuePair<ReadWriteSetKey, string> read in toMerge.ReadSet.ToList())
            {
                this.AddReadItem(read.Key, read.Value);
            }
        }

        public void MergeWriteSet(ReadWriteSet toMerge)
        {
            foreach (KeyValuePair<ReadWriteSetKey, byte[]> write in toMerge.WriteSet.ToList())
            {
                this.AddWriteItem(write.Key, write.Value);
            }
        }

        public string ToJsonString()
        {
            // TODO: Might be best off having something else do this.
            return JsonConvert.SerializeObject(new ReadWriteSetDto(this));
        }
    }
}
