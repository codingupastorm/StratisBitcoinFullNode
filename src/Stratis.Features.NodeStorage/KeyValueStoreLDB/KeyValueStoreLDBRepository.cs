using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LevelDB;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;

namespace Stratis.Features.NodeStorage.KeyValueStoreLDB
{
    public class KeyValueStoreLDBRepository : KeyValueStoreRepository
    {
        private class KeyValueStoreLDBTransaction : KeyValueStoreTransaction
        {
            public SnapShot snapshot;
            public ReadOptions readOptions => (this.snapshot == null) ? new ReadOptions() : new ReadOptions() { Snapshot = this.snapshot };

            public KeyValueStoreLDBTransaction(IKeyValueStoreRepository repository, KeyValueStoreTransactionMode mode, params string[] tables)
                : base(repository, mode, tables)
            {
            }

            public ConcurrentBag<string> TablesCleared => this.tablesCleared;
            public ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>> TableUpdates => this.tableUpdates;
        }

        /// <summary>
        /// LevelDB does not understand the concept of tables. However this class introduces that concept in a way the LevelDB can understand.
        /// </summary>
        /// <remarks>
        /// The standard workaround is to prefix the key with the "table" identifier.
        /// </remarks>
        private class KeyValueStoreLDBTable : IKeyValueStoreTable
        {
            public string TableName { get; internal set; }
            public byte KeyPrefix { get; internal set; }
            public KeyValueStoreLDBRepository Repository { get; internal set; }
        }

        private DB Storage;
        private int nextTablePrefix;
        private SingleThreadResource TransactionLock;

        public KeyValueStoreLDBRepository(KeyValueStore.KeyValueStore keyValueStore) : base(keyValueStore)
        {
            var logger = this.KeyValueStore.LoggerFactory.CreateLogger(nameof(KeyValueStoreLDBRepository));

            this.TransactionLock = new SingleThreadResource($"{nameof(this.TransactionLock)}", logger);
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

        public override byte[] Get(IKeyValueStoreTransaction tran, IKeyValueStoreTable table, byte[] key)
        {
            var keyBytes = new byte[] { ((KeyValueStoreLDBTable)table).KeyPrefix }.Concat(key).ToArray();

            return this.Storage.Get(keyBytes, ((KeyValueStoreLDBTransaction)tran).readOptions);
        }

        public override IEnumerable<(byte[], byte[])> GetAll(IKeyValueStoreTransaction tran, IKeyValueStoreTable table, bool keysOnly = false)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).readOptions))
            {
                iterator.SeekToFirst();

                while (iterator.IsValid())
                {
                    byte[] keyBytes = iterator.Key();

                    if (keyBytes[0] == ((KeyValueStoreLDBTable)table).KeyPrefix)
                        yield return (keyBytes.Skip(1).ToArray(), keysOnly ? null : iterator.Value());

                    iterator.Next();
                }
            }
        }

        public override IKeyValueStoreTable GetTable(string tableName)
        {
            if (!this.Tables.TryGetValue(tableName, out IKeyValueStoreTable table))
            {
                table = new KeyValueStoreLDBTable()
                {
                    Repository = this,
                    TableName = tableName,
                    KeyPrefix = (byte)this.nextTablePrefix++
                };

                this.Storage.Put(new byte[] { 0, ((KeyValueStoreLDBTable)table).KeyPrefix }, Encoding.ASCII.GetBytes(((KeyValueStoreLDBTable)table).TableName));

                this.Tables[tableName] = table;
            }

            return table;
        }

        public override IKeyValueStoreTransaction StartTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return new KeyValueStoreLDBTransaction(this, mode, tables);
        }

        public override void OnBeginTransaction(IKeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode)
        {
            if (mode == KeyValueStoreTransactionMode.ReadWrite)
            {
                this.TransactionLock.Wait();
            }

            ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).snapshot = (mode == KeyValueStoreTransactionMode.Read) ? this.Storage.CreateSnapshot() : null;
        }

        public override void OnCommit(IKeyValueStoreTransaction keyValueStoreTransaction)
        {
            try
            {
                var writeBatch = new WriteBatch();

                foreach (string tableName in ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).TablesCleared)
                {
                    var table = (KeyValueStoreLDBTable)this.GetTable(tableName);

                    foreach ((byte[] Key, byte[] _) kv in this.GetAll(keyValueStoreTransaction, table, true))
                    {
                        writeBatch.Delete(new byte[] { table.KeyPrefix }.Concat(kv.Key).ToArray());
                    }
                }

                foreach (KeyValuePair<string, ConcurrentDictionary<byte[], byte[]>> updates in ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).TableUpdates)
                {
                    var table = (KeyValueStoreLDBTable)this.GetTable(updates.Key);

                    foreach (KeyValuePair<byte[], byte[]> kv in updates.Value)
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
                ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).snapshot?.Dispose();
                this.TransactionLock.Release();
            }
        }

        public override void OnRollback(IKeyValueStoreTransaction keyValueStoreTransaction)
        {
            ((KeyValueStoreLDBTransaction)keyValueStoreTransaction).snapshot?.Dispose();
            this.TransactionLock.Release();
        }

        public override void Close()
        {
            this.Storage.Close();
        }
    }
}
