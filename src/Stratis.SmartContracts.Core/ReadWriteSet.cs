using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Core
{
    public class ReadWriteSet
    {
        // TODO: Nested execution!

        // TODO: These types are a little dirty. Can we do something nicer than IReadOnlyCollection?

        /// <summary>
        /// Key: {storageKey}, Value: {version}.
        /// </summary>
        private readonly Dictionary<string, string> readSet;

        /// <summary>
        /// Key: {storageKey}, Value: {writtenBytes}.
        /// </summary>
        private readonly Dictionary<string, byte[]> writeSet;

        public IReadOnlyCollection<KeyValuePair<string, string>> ReadSet => this.readSet;

        public IReadOnlyCollection<KeyValuePair<string, byte[]>> WriteSet => this.writeSet;

        public ReadWriteSet()
        {
            this.readSet = new Dictionary<string, string>();
            this.writeSet = new Dictionary<string, byte[]>();
        }

        public void AddReadItem(string key, string version)
        {
            // If an item is already in the list, don't add it again.
            if (this.readSet.ContainsKey(key))
                return;

            // If an item is already in the write set, don't add it. We would be reading that value, not the previous version's' value.
            if (this.writeSet.ContainsKey(key))
                return;

            this.readSet[key] = version;
        }

        public void AddWriteItem(string key, byte[] value)
        {
            // Always store the last value for every key. We clone to avoid issues where the byte array might be altered afterwards.
            byte[] clonedValue = new byte[value.Length];
            Array.Copy(value, clonedValue, value.Length);
            this.writeSet[key] = clonedValue;
        }
    }
}
