using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBreeze.Utils;
using LevelDB;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;

namespace Stratis.Features.NodeStorage.KeyValueStoreLevelDB
{
    public class KeyValueStoreLevelDB : KeyValueStoreRepository
    {
        private class KeyValueStoreLDBTransaction : KeyValueStoreTransaction
        {
            public SnapShot Snapshot;
            public ReadOptions ReadOptions => (this.Snapshot == null) ? new ReadOptions() : new ReadOptions() { Snapshot = this.Snapshot };

            public KeyValueStoreLDBTransaction(IKeyValueStoreRepository repository, KeyValueStoreTransactionMode mode, params string[] tables)
                : base(repository, mode, tables)
            {
            }

            internal ConcurrentBag<string> TablesCleared => this.tablesCleared;
            internal ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>> TableUpdates => this.tableUpdates;
        }

        /// <summary>
        /// LevelDB does not understand the concept of tables. However this class introduces that concept in a way the LevelDB can understand.
        /// </summary>
        /// <remarks>
        /// The standard workaround is to prefix the key with the "table" identifier.
        /// </remarks>
        private class KeyValueStoreLDBTable : KeyValueStoreTable
        {
            public byte KeyPrefix { get; internal set; }
        }

        private DB Storage;
        private int nextTablePrefix;
        private SingleThreadResource TransactionLock;
        private ByteListComparer byteListComparer;

        public KeyValueStoreLevelDB(KeyValueStore.KeyValueStore keyValueStore) : base(keyValueStore)
        {
            var logger = this.KeyValueStore.LoggerFactory.CreateLogger(nameof(KeyValueStoreLevelDB));

            this.TransactionLock = new SingleThreadResource($"{nameof(this.TransactionLock)}", logger);
            this.byteListComparer = new ByteListComparer();
        }

        public override void Init(string rootPath)
        {
            var options = new Options()
            {
                CreateIfMissing = true,
            };

            this.Storage = new DB(options, rootPath);

            for (this.nextTablePrefix = 1; ; this.nextTablePrefix++)
            {
                byte[] tableNameBytes = this.Storage.Get(new byte[] { 0, (byte)this.nextTablePrefix });
                if (tableNameBytes == null)
                    break;

                string tableName = Encoding.ASCII.GetString(tableNameBytes);
                this.Tables[tableName] = new KeyValueStoreLDBTable()
                {
                    Repository = this,
                    TableName = tableName,
                    KeyPrefix = (byte)this.nextTablePrefix
                };
            }
        }

        public override int Count(KeyValueStoreTransaction tran, KeyValueStoreTable table)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).ReadOptions))
            {
                int count = 0;

                iterator.SeekToFirst();

                while (iterator.IsValid())
                {
                    byte[] keyBytes = iterator.Key();

                    if (keyBytes[0] == ((KeyValueStoreLDBTable)table).KeyPrefix)
                        count++;

                    iterator.Next();
                }

                return count;
            }
        }

        public override bool[] Exists(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).ReadOptions))
            {
                byte keyPrefix = ((KeyValueStoreLDBTable)table).KeyPrefix;

                bool Exist(byte[] key)
                {
                    var keyBytes = new byte[] { keyPrefix }.Concat(key).ToArray();
                    iterator.Seek(keyBytes);
                    return iterator.IsValid() && this.byteListComparer.Compare(iterator.Key(), keyBytes) == 0;
                }

                (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, this.byteListComparer).ToArray();
                var exists = new bool[keys.Length];
                for (int i = 0; i < orderedKeys.Length; i++)
                    exists[orderedKeys[i].n] = Exist(orderedKeys[i].k);

                return exists;
            }
        }

        public override byte[][] Get(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys)
        {
            var keyBytes = keys.Select(key => new byte[] { ((KeyValueStoreLDBTable)table).KeyPrefix }.Concat(key).ToArray()).ToArray();
            (byte[] k, int n)[] orderedKeys = keyBytes.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteListComparer()).ToArray();
            var res = new byte[keys.Length][];
            for (int i = 0; i < orderedKeys.Length; i++)
                res[orderedKeys[i].n] = this.Storage.Get(orderedKeys[i].k, ((KeyValueStoreLDBTransaction)tran).ReadOptions);

            return res;
        }

        public override IEnumerable<(byte[], byte[])> GetAll(KeyValueStoreTransaction tran, KeyValueStoreTable table, bool keysOnly = false, bool backwards = false)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).ReadOptions))
            {
                if (backwards)
                    iterator.SeekToLast();
                else
                    iterator.SeekToFirst();

                while (iterator.IsValid())
                {
                    byte[] keyBytes = iterator.Key();

                    if (keyBytes[0] == ((KeyValueStoreLDBTable)table).KeyPrefix)
                        yield return (keyBytes.Skip(1).ToArray(), keysOnly ? null : iterator.Value());

                    if (backwards)
                        iterator.Prev();
                    else
                        iterator.Next();
                }
            }
        }

        public override KeyValueStoreTable GetTable(string tableName)
        {
            if (!this.Tables.TryGetValue(tableName, out KeyValueStoreTable table))
            {
                table = new KeyValueStoreLDBTable()
                {
                    Repository = this,
                    TableName = tableName,
                    KeyPrefix = (byte)this.nextTablePrefix++
                };

                this.Storage.Put(new byte[] { 0, ((KeyValueStoreLDBTable)table).KeyPrefix }, Encoding.ASCII.GetBytes(table.TableName));

                this.Tables[tableName] = table;
            }

            return table;
        }

        public override KeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return new KeyValueStoreLDBTransaction(this, mode, tables);
        }

        public override void OnBeginTransaction(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode)
        {
            if (mode == KeyValueStoreTransactionMode.ReadWrite)
            {
                this.TransactionLock.Wait();
            }

            ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).Snapshot = (mode == KeyValueStoreTransactionMode.Read) ? this.Storage.CreateSnapshot() : null;
        }

        public override void OnCommit(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            try
            {
                var writeBatch = new WriteBatch();
                var tableUpdates = ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).TableUpdates;

                foreach (string tableName in ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).TablesCleared)
                {
                    var table = (KeyValueStoreLDBTable)this.GetTable(tableName);
                    tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> tableUpdate);

                    foreach ((byte[] Key, byte[] _) kv in this.GetAll(keyValueStoreTransaction, table, true))
                    {
                        if (tableUpdate != null && tableUpdate.ContainsKey(kv.Key))
                            continue;

                        writeBatch.Delete(new byte[] { table.KeyPrefix }.Concat(kv.Key).ToArray());
                    }
                }

                foreach (KeyValuePair<string, ConcurrentDictionary<byte[], byte[]>> tableUpdate in tableUpdates)
                {
                    var table = (KeyValueStoreLDBTable)this.GetTable(tableUpdate.Key);

                    foreach (KeyValuePair<byte[], byte[]> kv in tableUpdate.Value)
                    {
                        if (kv.Value == null)
                        {
                            writeBatch.Delete(new byte[] { table.KeyPrefix }.Concat(kv.Key).ToArray());
                        }
                        else
                        {
                            writeBatch.Put(new byte[] { table.KeyPrefix }.Concat(kv.Key).ToArray(), kv.Value);
                        }
                    }
                }

                this.Storage.Write(writeBatch, new WriteOptions() { Sync = true });
            }
            finally
            {
                ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).Snapshot?.Dispose();
                this.TransactionLock.Release();
            }
        }

        public override void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).Snapshot?.Dispose();
            this.TransactionLock.Release();
        }

        public override void Close()
        {
            this.Storage.Close();
        }
    }
}
