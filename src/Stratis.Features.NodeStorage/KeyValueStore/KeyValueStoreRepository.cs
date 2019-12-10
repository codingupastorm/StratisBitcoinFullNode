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
        public Dictionary<string, IKeyValueStoreTable> Tables { get; protected set; }

        public KeyValueStoreRepository(KeyValueStore keyValueStore)
        {
            this.KeyValueStore = keyValueStore;
            this.Tables = new Dictionary<string, IKeyValueStoreTable>();
        }

        /// <inheritdoc />
        public abstract IKeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        public abstract int Count(IKeyValueStoreTransaction tran, IKeyValueStoreTable table);

        /// <inheritdoc />
        public abstract bool[] Exists(IKeyValueStoreTransaction tran, IKeyValueStoreTable table, byte[][] keys);

        /// <inheritdoc />
        public abstract byte[][] Get(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable, byte[][] keys);

        /// <inheritdoc />
        public abstract IEnumerable<(byte[], byte[])> GetAll(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable, bool keysOnly = false);

        /// <inheritdoc />
        public abstract void Init(string rootPath);

        /// <inheritdoc />
        public abstract void OnBeginTransaction(IKeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode);

        /// <inheritdoc />
        public abstract void OnCommit(IKeyValueStoreTransaction keyValueStoreTransaction);

        /// <inheritdoc />
        public abstract void OnRollback(IKeyValueStoreTransaction keyValueStoreTransaction);

        /// <inheritdoc />
        public abstract IKeyValueStoreTable GetTable(string tableName);

        /// <inheritdoc />
        public abstract void Close();
    }
}
