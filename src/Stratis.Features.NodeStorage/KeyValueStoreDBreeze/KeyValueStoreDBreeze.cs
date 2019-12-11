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

        public override int Count(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            return (int)dbTransaction.Count(table.TableName);
        }

        public override bool[] Exists(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table, byte[][] keys)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            dbTransaction.ValuesLazyLoadingIsOn = true;
            try
            {
                (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteListComparer()).ToArray();

                var exists = new bool[keys.Length];
                for (int i = 0; i < orderedKeys.Length; i++)
                    exists[orderedKeys[i].n] = dbTransaction.Select<byte[], byte[]>(table.TableName, orderedKeys[i].k).Exists;

                return exists;
            }
            finally
            {
                dbTransaction.ValuesLazyLoadingIsOn = false;
            }
        }

        public override byte[][] Get(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table, byte[][] keys)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteListComparer()).ToArray();
            var res = new byte[keys.Length][];
            for (int i = 0; i < orderedKeys.Length; i++)
            {
                var key = orderedKeys[i].k;
                var row = dbTransaction.Select<byte[], byte[]>(table.TableName, key);
                res[orderedKeys[i].n] = row.Exists ? row.Value : null;
            }

            return res;
        }

        public override IEnumerable<(byte[], byte[])> GetAll(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table, bool keysOnly, bool backwards = false)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            dbTransaction.ValuesLazyLoadingIsOn = keysOnly;

            try
            {
                if (backwards)
                {
                    foreach (var row in dbTransaction.SelectBackward<byte[], byte[]>(table.TableName))
                    {
                        yield return (row.Key, row.Value);
                    }

                }
                else
                {
                    foreach (var row in dbTransaction.SelectForward<byte[], byte[]>(table.TableName))
                    {
                        yield return (row.Key, row.Value);
                    }
                }
            }
            finally
            {
                dbTransaction.ValuesLazyLoadingIsOn = false;
            }
        }

        public override KeyValueStoreTable GetTable(string tableName)
        {
            if (!this.Tables.TryGetValue(tableName, out KeyValueStoreTable table))
            {
                table = new KeyValueStoreTable()
                {
                    Repository = this,
                    TableName = tableName
                };

                this.Tables[tableName] = table;
            }

            return table;
        }

        public override KeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return new KeyValueStoreDBZTransaction(this, mode, tables);
        }

        public override void OnBeginTransaction(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode)
        {
            if (mode == KeyValueStoreTransactionMode.ReadWrite)
            {
                this.TransactionLock.Wait();
            }
        }

        public override void OnCommit(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            var tablesModified = tran.TablesCleared.Concat(tran.TableUpdates.Keys).Distinct().ToArray();
            if (tablesModified.Length > 0)
                dbTransaction.SynchronizeTables(tablesModified);

            try
            {
                foreach (string tableName in tran.TablesCleared)
                {
                    var table = this.GetTable(tableName);

                    dbTransaction.RemoveAllKeys(tableName, true);
                }

                foreach (KeyValuePair<string, ConcurrentDictionary<byte[], byte[]>> updates in tran.TableUpdates)
                {
                    var table = this.GetTable(updates.Key);

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

        public override void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.dBreezeTransaction;

            dbTransaction.Rollback();
            dbTransaction.Dispose();

            this.TransactionLock.Release();
        }

        public override void Close()
        {
        }
    }
}
