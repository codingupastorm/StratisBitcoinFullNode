using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LevelDB;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStoreLevelDB
{
    public class KeyValueStoreLevelDB : IKeyValueStoreRepository
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

        public KeyValueStoreLevelDB(string rootPath, ILoggerFactory loggerFactory,
            IRepositorySerializer repositorySerializer)
        {
            var logger = loggerFactory.CreateLogger(nameof(KeyValueStoreLevelDB));

            this.transactionLock = new SingleThreadResource($"{nameof(this.transactionLock)}", logger);
            this.byteArrayComparer = new ByteArrayComparer();
            this.RepositorySerializer = repositorySerializer;
            this.Tables = new Dictionary<string, KeyValueStoreTable>();
            this.Init(rootPath);
        }

        public IRepositorySerializer RepositorySerializer { get; }

        public Dictionary<string, KeyValueStoreTable> Tables { get; }

        /// <summary>
        /// Initialize the underlying database / glue-layer.
        /// </summary>
        /// <param name="rootPath">The location of the key-value store.</param>
        private void Init(string rootPath)
        {
            var options = new Options()
            {
                CreateIfMissing = true,
            };

            this.Close();

            Directory.CreateDirectory(rootPath);

            try
            {
                this.Storage = new DB(options, rootPath);
            }
            catch (Exception err)
            {
                throw new Exception($"An error occurred while attempting to open the LevelDB database at '{rootPath}': {err.Message}'", err);
            }

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

        public int Count(KeyValueStoreTransaction tran, KeyValueStoreTable table)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).ReadOptions))
            {
                int count = 0;

                byte keyPrefix = ((KeyValueStoreLDBTable)table).KeyPrefix;

                iterator.Seek(new[] { keyPrefix });

                while (iterator.IsValid())
                {
                    byte[] keyBytes = iterator.Key();

                    if (keyBytes[0] != keyPrefix)
                        break;

                    count++;

                    iterator.Next();
                }

                return count;
            }
        }

        public bool[] Exists(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys)
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

        public byte[][] Get(KeyValueStoreTransaction tran, KeyValueStoreTable table, byte[][] keys)
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

        public IEnumerable<(byte[], byte[])> GetAll(KeyValueStoreTransaction tran, KeyValueStoreTable table, bool keysOnly = false, SortOrder sortOrder = SortOrder.Ascending,
            byte[] firstKey = null, byte[] lastKey = null, bool includeFirstKey = true, bool includeLastKey = true)
        {
            using (Iterator iterator = this.Storage.CreateIterator(((KeyValueStoreLDBTransaction)tran).ReadOptions))
            {
                byte keyPrefix = ((KeyValueStoreLDBTable)table).KeyPrefix;
                byte[] firstKeyBytes = (firstKey == null) ? null : new[] { keyPrefix }.Concat(firstKey).ToArray();
                byte[] lastKeyBytes = (lastKey == null) ? null : new[] { keyPrefix }.Concat(lastKey).ToArray();

                if (sortOrder == SortOrder.Descending)
                {
                    if (lastKeyBytes == null)
                    {
                        iterator.Seek(new[] { (byte)(keyPrefix + 1) });
                        if (iterator.IsValid())
                            iterator.Prev();
                        else
                            iterator.SeekToLast();
                    }
                    else
                    {
                        iterator.Seek(lastKeyBytes);
                        if (!includeLastKey && this.byteArrayComparer.Equals(iterator.Key(), lastKeyBytes))
                            iterator.Prev();
                    }
                }
                else
                {
                    if (firstKeyBytes == null)
                    {
                        iterator.Seek(new[] { keyPrefix });
                    }
                    else
                    {
                        iterator.Seek(firstKeyBytes);
                        if (!includeFirstKey && this.byteArrayComparer.Equals(iterator.Key(), firstKeyBytes))
                            iterator.Next();
                    }
                }

                bool done = false;
                while (iterator.IsValid())
                {
                    byte[] keyBytes = iterator.Key();

                    if (keyBytes[0] != keyPrefix)
                        break;

                    if (sortOrder == SortOrder.Descending)
                    {
                        if (firstKeyBytes != null && this.byteArrayComparer.Compare(keyBytes, firstKeyBytes) <= 0)
                        {
                            if (!includeFirstKey)
                                break;
                            done = true;
                        }
                    }
                    else
                    {
                        if (lastKeyBytes != null && this.byteArrayComparer.Compare(keyBytes, lastKeyBytes) >= 0)
                        {
                            if (!includeLastKey)
                                break;
                            done = true;
                        }
                    }

                    yield return (keyBytes.Skip(1).ToArray(), keysOnly ? null : iterator.Value());

                    if (done)
                        break;

                    if (sortOrder == SortOrder.Descending)
                        iterator.Prev();
                    else
                        iterator.Next();
                }
            }
        }

        public KeyValueStoreTable GetTable(string tableName)
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

        public KeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return new KeyValueStoreLDBTransaction(this, mode, tables);
        }

        public void OnBeginTransaction(KeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode)
        {
            if (mode == KeyValueStoreTransactionMode.ReadWrite)
            {
                this.transactionLock.Wait();
            }
        }

        public void OnCommit(KeyValueStoreTransaction keyValueStoreTransaction)
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

        public void OnRollback(KeyValueStoreTransaction keyValueStoreTransaction)
        {
            this.transactionLock.Release();
        }

        public void Close()
        {
            this.Storage?.Dispose();
            this.Storage = null;
        }

        public string[] GetTables()
        {
            return this.Tables.Select(t => t.Value.TableName).ToArray();
        }

        public IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return this.CreateKeyValueStoreTransaction(mode, tables);
        }

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

        public byte[] Serialize<T>(T obj)
        {
            return this.RepositorySerializer.Serialize(obj);
        }

        public T Deserialize<T>(byte[] objBytes)
        {
            return this.RepositorySerializer.Deserialize<T>(objBytes);
        }
    }
}
