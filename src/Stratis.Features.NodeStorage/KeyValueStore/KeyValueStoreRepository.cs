using System.Collections.Generic;
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

        /// <inheritdoc />
        public abstract KeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        public abstract int Count(KeyValueStoreTransaction tran, KeyValueStoreTable table);

        /// <inheritdoc />
        public abstract bool[] Exists(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys);

        /// <inheritdoc />
        public abstract byte[][] Get(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable keyValueStoreTable, byte[][] keys);

        /// <inheritdoc />
        public abstract IEnumerable<(byte[], byte[])> GetAll(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable keyValueStoreTable, bool keysOnly = false);

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
