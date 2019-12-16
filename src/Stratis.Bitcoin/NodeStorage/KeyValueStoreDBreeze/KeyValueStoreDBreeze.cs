using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DBreeze;
using DBreeze.Utils;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStoreDBreeze
{
    public class KeyValueStoreDBreeze : KeyValueStoreRepository
    {
        internal class KeyValueStoreDBZTransaction : KeyValueStoreTransaction
        {
            public DBreeze.Transactions.Transaction DBreezeTransaction { get; private set; }

            public KeyValueStoreDBZTransaction(KeyValueStoreDBreeze repository, KeyValueStoreTransactionMode mode, params string[] tables)
                : base(repository, mode, tables)
            {
                this.DBreezeTransaction = repository.storage.GetTransaction();
                if (mode == KeyValueStoreTransactionMode.Read && tables.Length > 0)
                    this.DBreezeTransaction.SynchronizeTables(tables);
            }
        }

        private DBreezeEngine storage;
        private SingleThreadResource transactionLock;

        public KeyValueStoreDBreeze(KeyValueStore.KeyValueStore keyValueStore)
            : base(keyValueStore)
        {
            var logger = keyValueStore.LoggerFactory.CreateLogger(nameof(KeyValueStoreLevelDB));

            this.transactionLock = new SingleThreadResource($"{nameof(this.transactionLock)}", logger);
        }

        public override T Deserialize<T>(byte[] objBytes)
        {
            if (typeof(T).IsValueType)
                return (T)(object)DBreeze.DataTypes.DataTypesConvertor.ConvertBack<T>(objBytes);

            return base.Deserialize<T>(objBytes);
        }

        public override byte[] Serialize<T>(T obj)
        {
            if (typeof(T).IsValueType)
                return ((T)obj).ToBytes();

            return base.Serialize(obj);
        }

        public override void Init(string rootPath)
        {
            this.Close();
            this.storage = new DBreezeEngine(rootPath);
        }

        public override int Count(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.DBreezeTransaction;

            return (int)dbTransaction.Count(table.TableName);
        }

        public override bool[] Exists(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTable table, byte[][] keys)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.DBreezeTransaction;

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
            var dbTransaction = tran.DBreezeTransaction;

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
            var dbTransaction = tran.DBreezeTransaction;

            dbTransaction.ValuesLazyLoadingIsOn = keysOnly;

            try
            {
                if (backwards)
                {
                    foreach (var row in dbTransaction.SelectBackward<byte[], byte[]>(table.TableName))
                    {
                        yield return (row.Key, keysOnly ? null : row.Value);
                    }
                }
                else
                {
                    foreach (var row in dbTransaction.SelectForward<byte[], byte[]>(table.TableName))
                    {
                        yield return (row.Key, keysOnly ? null : row.Value);
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
                this.transactionLock.Wait();
            }
        }

        public override void OnCommit(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.DBreezeTransaction;

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

                this.transactionLock.Release();
            }
        }

        public override void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            var tran = (KeyValueStoreDBZTransaction)keyValueStoreTransaction;
            var dbTransaction = tran.DBreezeTransaction;

            dbTransaction.Rollback();
            dbTransaction.Dispose();

            this.transactionLock.Release();
        }

        public override void Close()
        {
            this.storage?.Dispose();
            this.storage = null;
        }
    }
}
