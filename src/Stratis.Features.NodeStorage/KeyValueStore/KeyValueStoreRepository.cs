using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Features.NodeStorage.Interfaces;

namespace Stratis.Features.NodeStorage.KeyValueStore
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
            if (obj == null)
                return new byte[] { };

            if (obj.GetType() == typeof(byte[]))
                return (byte[])(object)obj;

            if (obj.GetType() == typeof(bool) || obj.GetType() == typeof(bool?))
                return new byte[] { (byte)((bool)(object)obj ? 1 : 0) };

            if (obj.GetType() == typeof(int))
            {
                byte[] bytes = BitConverter.GetBytes((int)(object)obj);
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return bytes;
            }

            return this.KeyValueStore.RepositorySerializer.Serialize(obj);
        }

        public virtual T Deserialize<T>(byte[] objBytes)
        {
            if (objBytes == null)
                return default(T);

            Type objType = typeof(T);

            if (objType == typeof(byte[]))
                return (T)(object)objBytes;

            if (objBytes.Length == 0)
                return default(T);

            if (objType == typeof(bool) || objType == typeof(bool?))
                return (T)(object)(objBytes[0] != 0);

            if (objType == typeof(int))
            {
                var bytes = (byte[])objBytes.Clone();
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return (T)(object)BitConverter.ToInt32(bytes, 0);
            }

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
    }
}
