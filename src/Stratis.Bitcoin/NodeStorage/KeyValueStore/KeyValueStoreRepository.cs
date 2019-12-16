using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStore
{
    /// <summary>
    /// Abstract representation of the storage / underlying database type.
    /// </summary>
    public abstract class KeyValueStoreRepository : IKeyValueStoreRepository
    {
        public KeyValueStore KeyValueStore { get; protected set; }

        public Dictionary<string, KeyValueStoreTable> Tables { get; protected set; }

        public KeyValueStoreRepository(KeyValueStore keyValueStore)
        {
            this.KeyValueStore = keyValueStore;
            this.Tables = new Dictionary<string, KeyValueStoreTable>();
        }

        public virtual byte[] Serialize<T>(T obj)
        {
            if (typeof(T) == typeof(byte[]))
                return (byte[])(object)obj;

            if (obj == null)
                return new byte[] { };

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return new byte[] { (byte)((bool)(object)obj ? 1 : 0) };

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                byte[] bytes = BitConverter.GetBytes((int)(object)obj);
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return bytes;
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                byte[] bytes = BitConverter.GetBytes((uint)(object)obj);
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return bytes;
            }

            Guard.Assert(!typeof(T).IsValueType);

            return this.KeyValueStore.RepositorySerializer.Serialize(obj);
        }

        public virtual T Deserialize<T>(byte[] objBytes)
        {
            if (objBytes == null)
                return default;

            if (typeof(T) == typeof(byte[]))
                return (T)(object)objBytes;

            if (objBytes.Length == 0)
                return default;

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return (T)(object)(objBytes[0] != 0);

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                var bytes = (byte[])objBytes.Clone();
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return (T)(object)BitConverter.ToInt32(bytes, 0);
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                var bytes = (byte[])objBytes.Clone();
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return (T)(object)BitConverter.ToUInt32(bytes, 0);
            }

            Guard.Assert(!typeof(T).IsValueType);

            return (T)this.KeyValueStore.RepositorySerializer.Deserialize(objBytes, typeof(T));
        }

        /// <inheritdoc />
        public abstract KeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        public abstract int Count(KeyValueStoreTransaction tran, KeyValueStoreTable table);

        /// <inheritdoc />
        public abstract bool[] Exists(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys);

        /// <inheritdoc />
        public abstract byte[][] Get(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable keyValueStoreTable, byte[][] keys);

        /// <inheritdoc />
        public abstract IEnumerable<(byte[], byte[])> GetAll(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable keyValueStoreTable, bool keysOnly = false, bool backwards = false);

        /// <inheritdoc />
        public abstract void Init(string rootPath);

        /// <inheritdoc />
        public abstract void OnBeginTransaction(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode);

        /// <inheritdoc />
        public abstract void OnCommit(KeyValueStoreTransaction keyValueStoreTransaction);

        /// <inheritdoc />
        public abstract void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction);

        /// <inheritdoc />
        public abstract KeyValueStoreTable GetTable(string tableName);

        /// <inheritdoc />
        public abstract void Close();

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Protected implementation of Dispose pattern.</summary>
        /// <param name="disposing">Indicates whether disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                this.Close();
        }
    }
}
