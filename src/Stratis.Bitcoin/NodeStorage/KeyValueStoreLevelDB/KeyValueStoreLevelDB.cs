using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LevelDB;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStoreLevelDB
{
    public class KeyValueStoreLevelDB : KeyValueStoreRepository
    {
        internal class KeyValueStoreLDBTransaction : KeyValueStoreTransaction
        {
            public SnapShot Snapshot { get; private set; }

            public ReadOptions ReadOptions => (this.Snapshot == null) ? new ReadOptions() : new ReadOptions() { Snapshot = this.Snapshot };

            public KeyValueStoreLDBTransaction(KeyValueStoreLevelDB repository, KeyValueStoreTransactionMode mode, params string[] tables)
                : base(repository, mode, tables)
            {
                this.Snapshot = (mode == KeyValueStoreTransactionMode.Read) ? repository.Storage.CreateSnapshot() : null;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    this.Snapshot?.Dispose();

                base.Dispose(disposing);
            }
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

        internal DB Storage { get; private set; }

        private int nextTablePrefix;
        private SingleThreadResource transactionLock;
        private ByteArrayComparer byteArrayComparer;

        public KeyValueStoreLevelDB(KeyValueStore.KeyValueStore keyValueStore) : base(keyValueStore.RepositorySerializer)
        {
            var logger = keyValueStore.LoggerFactory.CreateLogger(nameof(KeyValueStoreLevelDB));

            this.transactionLock = new SingleThreadResource($"{nameof(this.transactionLock)}", logger);
            this.byteArrayComparer = new ByteArrayComparer();
        }

        public override void Init(string rootPath)
        {
            var options = new Options()
            {
                CreateIfMissing = true,
            };

            this.Close();

            Directory.CreateDirectory(rootPath);
            this.Storage = new DB(options, rootPath);

            Guard.NotNull(this.Storage, nameof(this.Storage));

            this.Tables.Clear();
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
                    return iterator.IsValid() && this.byteArrayComparer.Equals(iterator.Key(), keyBytes);
                }

                (byte[] k, int n)[] orderedKeys = keys.Select((k, n) => (k, n)).OrderBy(t => t.k, this.byteArrayComparer).ToArray();
                var exists = new bool[keys.Length];
                for (int i = 0; i < orderedKeys.Length; i++)
                    exists[orderedKeys[i].n] = Exist(orderedKeys[i].k);

                return exists;
            }
        }

        public override byte[][] Get(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys)
        {
            var keyBytes = keys.Select(key => new byte[] { ((KeyValueStoreLDBTable)table).KeyPrefix }.Concat(key).ToArray()).ToArray();
            (byte[] k, int n)[] orderedKeys = keyBytes.Select((k, n) => (k, n)).OrderBy(t => t.k, new ByteArrayComparer()).ToArray();
            var res = new byte[keys.Length][];
            for (int i = 0; i < orderedKeys.Length; i++)
            {
                if (orderedKeys[i].k == null)
                    continue;

                res[orderedKeys[i].n] = this.Storage.Get(orderedKeys[i].k, ((KeyValueStoreLDBTransaction)tran).ReadOptions);
            }

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
                this.transactionLock.Wait();
            }
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
                this.transactionLock.Release();
            }
        }

        public override void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            this.transactionLock.Release();
        }

        public override void Close()
        {
            this.Storage?.Dispose();
            this.Storage = null;
        }
    }
}
