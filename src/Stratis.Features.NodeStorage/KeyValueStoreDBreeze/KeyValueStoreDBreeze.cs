using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DBreeze;
using DBreeze.Utils;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;

namespace Stratis.Features.NodeStorage.KeyValueStoreDBreeze
{
    public class KeyValueStoreDBreeze : KeyValueStoreRepository
    {
        private class KeyValueStoreDBZTransaction : KeyValueStoreTransaction
        {
            internal DBreeze.Transactions.Transaction dBreezeTransaction;

            public KeyValueStoreDBZTransaction(IKeyValueStoreRepository repository, KeyValueStoreTransactionMode mode, params string[] tables)
                : base(repository, mode, tables)
            {
                this.dBreezeTransaction = ((KeyValueStoreDBreeze)repository).Storage.GetTransaction();
                if (mode == KeyValueStoreTransactionMode.Read && tables.Length > 0)
                    this.dBreezeTransaction.SynchronizeTables(tables);
            }

            internal ConcurrentBag<string> TablesCleared => this.tablesCleared;
            internal ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>> TableUpdates => this.tableUpdates;
        }

        /// <summary>
        /// Information related to a DBreeze table.
        /// </summary>
        /// <remarks>
        /// The standard workaround is to prefix the key with the "table" identifier.
        /// </remarks>
        private class KeyValueStoreDBZTable : IKeyValueStoreTable
        {
            public string TableName { get; internal set; }
            public KeyValueStoreDBreeze Repository { get; internal set; }
        }

        private DBreezeEngine Storage;
        private SingleThreadResource TransactionLock;

        public KeyValueStoreDBreeze(KeyValueStore.KeyValueStore keyValueStore) : base(keyValueStore)
        {
            var logger = this.KeyValueStore.LoggerFactory.CreateLogger(nameof(KeyValueStoreLevelDB));

            this.TransactionLock = new SingleThreadResource($"{nameof(this.TransactionLock)}", logger);
        }

        public override void Init(string rootPath)
        {
            this.Storage = new DBreezeEngine(rootPath);
        }

        public override int Count(IKeyValueStoreTransaction tran, IKeyValueStoreTable table)
        {
            return (int)((KeyValueStoreDBZTransaction)tran).dBreezeTransaction.Count(((KeyValueStoreDBZTable)table).TableName);
        }

        public override bool[] Exists(IKeyValueStoreTransaction transaction, IKeyValueStoreTable table, byte[][] keys)
        {
            var tran = ((KeyValueStoreDBZTransaction)transaction).dBreezeTransaction;

            tran.ValuesLazyLoadingIsOn = true;
            try
            {
                (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteListComparer()).ToArray();

                var exists = new bool[keys.Length];
                for (int i = 0; i < orderedKeys.Length; i++)
                    exists[orderedKeys[i].n] = tran.Select<byte[], byte[]>(
                        ((KeyValueStoreDBZTable)table).TableName, orderedKeys[i].k).Exists;

                return exists;
            }
            finally
            {
                tran.ValuesLazyLoadingIsOn = false;
            }
        }

        public override byte[][] Get(IKeyValueStoreTransaction transaction, IKeyValueStoreTable table, byte[][] keys)
        {
            var tran = ((KeyValueStoreDBZTransaction)transaction).dBreezeTransaction;

            (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteListComparer()).ToArray();
            var res = new byte[keys.Length][];
            for (int i = 0; i < orderedKeys.Length; i++)
            {
                var key = orderedKeys[i].k;
                var row = tran.Select<byte[], byte[]>(((KeyValueStoreDBZTable)table).TableName, key);
                res[orderedKeys[i].n] = row.Exists ? row.Value : null;
            }

            return res;
        }

        public override IEnumerable<(byte[], byte[])> GetAll(IKeyValueStoreTransaction transaction, IKeyValueStoreTable table, bool keysOnly)
        {
            var tran = ((KeyValueStoreDBZTransaction)transaction).dBreezeTransaction;

            tran.ValuesLazyLoadingIsOn = keysOnly;

            try
            {
                foreach (var row in tran.SelectForward<byte[], byte[]>(((KeyValueStoreDBZTable)table).TableName))
                {
                    byte[] keyBytes = row.Key;

                    yield return (row.Key, row.Value);
                }
            }
            finally
            {
                tran.ValuesLazyLoadingIsOn = false;
            }
        }

        public override IKeyValueStoreTable GetTable(string tableName)
        {
            if (!this.Tables.TryGetValue(tableName, out IKeyValueStoreTable table))
            {
                table = new KeyValueStoreDBZTable()
                {
                    Repository = this,
                    TableName = tableName
                };

                this.Tables[tableName] = table;
            }

            return table;
        }

        public override IKeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return new KeyValueStoreDBZTransaction(this, mode, tables);
        }

        public override void OnBeginTransaction(IKeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode)
        {
            if (mode == KeyValueStoreTransactionMode.ReadWrite)
            {
                this.TransactionLock.Wait();
            }
        }

        public override void OnCommit(IKeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = ((KeyValueStoreDBZTransaction)keyValueStoreTransaction);
            var tablesModified = tran.TablesCleared.Concat(tran.TableUpdates.Keys).Distinct().ToArray();

            var dbTransaction = tran.dBreezeTransaction;
            if (tablesModified.Length > 0)
                dbTransaction.SynchronizeTables(tablesModified);

            try
            {
                foreach (string tableName in ((KeyValueStoreDBZTransaction)keyValueStoreTransaction).TablesCleared)
                {
                    var table = (KeyValueStoreDBZTable)this.GetTable(tableName);

                    dbTransaction.RemoveAllKeys(tableName, true);
                }

                foreach (KeyValuePair<string, ConcurrentDictionary<byte[], byte[]>> updates in ((KeyValueStoreDBZTransaction)keyValueStoreTransaction).TableUpdates)
                {
                    var table = (KeyValueStoreDBZTable)this.GetTable(updates.Key);

                    foreach (KeyValuePair<byte[], byte[]> kv in updates.Value)
                    {
                        if (kv.Value == null)
                        {
                            dbTransaction.RemoveKey(updates.Key, kv.Key);
                        }
                        else
                        {
                            dbTransaction.Insert(updates.Key, kv.Key, kv.Value);
                        }
                    }
                }
            }
            finally
            {
                dbTransaction.Commit();
                dbTransaction.Dispose();

                this.TransactionLock.Release();
            }
        }

        public override void OnRollback(IKeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = ((KeyValueStoreDBZTransaction)keyValueStoreTransaction).dBreezeTransaction;

            tran.Rollback();
            tran.Dispose();

            this.TransactionLock.Release();
        }

        public override void Close()
        {
        }
    }
}
