using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;

namespace Stratis.Features.NodeStorage.KeyValueStore
{
    /// <summary>
    /// An abstract representation of the underlying database transaction.
    /// Provides high-level methods, supporting serialization, to access the key-value store.
    /// </summary>
    /// <remarks>
    /// Changes are buffered in-memory until the commit operation takes place. This class also
    /// provides a mechanism to keep transient lookups (if any) in sync with changes to the database.
    /// </remarks>
    public abstract class KeyValueStoreTransaction : IKeyValueStoreTransaction
    {
        /// <summary>The underlying key-value repository provider.</summary>
        private IKeyValueStoreRepository Repository;

        /// <summary>Interface providing control over the updating of transient lookups.</summary>
        private readonly IKeyValueStoreTrackers lookups;

        /// <summary>The mode of the transaction.</summary>
        private readonly KeyValueStoreTransactionMode mode;

        /// <summary>Tracking changes allows updating of transient lookups after a successful commit operation.</summary>
        private Dictionary<string, IKeyValueStoreTracker> trackers;

        /// <summary>Used to buffer changes to records until a commit takes place.</summary>
        protected ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>> tableUpdates;

        /// <summary>Used to buffer clearing of table contents until a commit takes place.</summary>
        protected ConcurrentBag<string> tablesCleared;

        /// <summary>Comparer used to compare two byte arrays for equality.</summary>
        private IEqualityComparer<byte[]> byteArrayComparer = new ByteArrayComparer();

        /// <summary>Used to access attributes on repository's base-class.</summary>
        private KeyValueStoreRepository repository => (KeyValueStoreRepository)this.Repository;

        private bool isInTransaction;

        /// <summary>
        /// Creates a transction.
        /// </summary>
        /// <param name="keyValueStoreRepository">The database-specific storage.</param>
        /// <param name="mode">The mode in which to interact with the database.</param>
        /// <param name="tables">The tables being updated if any.</param>
        public KeyValueStoreTransaction(
            IKeyValueStoreRepository keyValueStoreRepository,
            KeyValueStoreTransactionMode mode,
            params string[] tables)
        {
            this.Repository = keyValueStoreRepository;
            this.mode = mode;
            this.lookups = this.repository.KeyValueStore.Lookups;
            this.trackers = this.repository.KeyValueStore.Lookups?.CreateTrackers(tables);
            this.tableUpdates = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>>();
            this.tablesCleared = new ConcurrentBag<string>();

            keyValueStoreRepository.OnBeginTransaction(this, mode);
            this.isInTransaction = true;
        }

        private IKeyValueStoreTable GetTable(string tableName)
        {
            return this.Repository.GetTable(tableName);
        }

        /// <inheritdoc />
        public int Count(string tableName)
        {
            return this.SelectForward(tableName, true).Count();
        }

        private byte[] Serialize<T>(T obj)
        {
            if (obj == null)
                return new byte[] { };

            if (obj.GetType() == typeof(byte[]))
                return (byte[])(object)obj;

            if (obj.GetType() == typeof(bool) || obj.GetType() == typeof(bool?))
                return new byte[] { (byte)((bool)(object)obj ? 1 : 0) };

            return this.repository.KeyValueStore.RepositorySerializer.Serialize(obj);
        }

        private T Deserialize<T>(byte[] objBytes)
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

            return (T)this.repository.KeyValueStore.RepositorySerializer.Deserialize(objBytes, typeof(T));
        }

        /// <inheritdoc />
        public void Insert<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            if (!this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                kv = new ConcurrentDictionary<byte[], byte[]>(this.byteArrayComparer);
                this.tableUpdates[tableName] = kv;
            }

            kv[this.Serialize(key)] = this.Serialize(obj);
        }

        /// <inheritdoc />
        public void InsertMultiple<TKey, TObject>(string tableName, (TKey, TObject)[] objects)
        {
            foreach ((TKey, TObject) kv in objects)
            {
                this.Insert(tableName, kv.Item1, kv.Item2);
            }
        }

        /// <inheritdoc />
        public void InsertDictionary<TKey, TObject>(string tableName, Dictionary<TKey, TObject> objects)
        {
            this.InsertMultiple(tableName, objects.Select(o => (o.Key, o.Value)).ToArray());
        }

        /// <inheritdoc />
        public bool Exists<TKey>(string tableName, TKey key)
        {
            var table = this.GetTable(tableName);
            var keyBytes = this.Serialize(key);

            if (this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv) && kv.TryGetValue(keyBytes, out byte[] res))
                return res != null;

            return this.Select<TKey, byte[]>(tableName, key, out _);
        }

        /// <inheritdoc />
        public bool[] ExistsMultiple<TKey>(string tableName, TKey[] keys)
        {
            return keys.Select(k => this.Exists(tableName, k)).ToArray();
        }

        /// <inheritdoc />
        public bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj)
        {
            var table = this.GetTable(tableName);
            var keyBytes = this.Serialize(key);

            if (!this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv) || !kv.TryGetValue(keyBytes, out byte[] res))
                res = this.tablesCleared.Contains(tableName) ? null : this.repository.Get(this, table, keyBytes);

            if (res == null)
            {
                obj = default(TObject);
                return false;
            }

            obj = this.Deserialize<TObject>(res.ToArray());

            return true;
        }

        /// <inheritdoc />
        public List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys)
        {
            TObject Select(TKey key)
            {
                this.Select(tableName, key, out TObject obj);
                return obj;
            }

            return keys.Select(k => Select(k)).ToList();
        }

        /// <inheritdoc />
        public Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName)
        {
            return this.SelectForward<TKey, TObject>(tableName).ToDictionary(kv => kv.Item1, kv => kv.Item2);
        }

        private IEnumerable<(byte[], byte[])> SelectForward(string tableName, bool keysOnly = false)
        {
            var table = this.GetTable(tableName);

            Dictionary<byte[], byte[]> res = this.tablesCleared.Contains(tableName) ?
                new Dictionary<byte[], byte[]>() :
                this.repository.GetAll(this, table, keysOnly).ToDictionary(k => k.Item1, k => k.Item2, this.byteArrayComparer);

            if (this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> updates))
                foreach (KeyValuePair<byte[], byte[]> kv in updates)
                    res[kv.Key] = kv.Value;

            foreach (KeyValuePair<byte[], byte[]> kv in res)
                if (kv.Value != null)
                    yield return (kv.Key, kv.Value);
        }

        /// <inheritdoc />
        public IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName)
        {
            foreach ((byte[], byte[]) kv in this.SelectForward(tableName))
                yield return (this.Deserialize<TKey>(kv.Item1), this.Deserialize<TObject>(kv.Item2));
        }

        /// <inheritdoc />
        public void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            var keyBytes = this.Serialize(key);

            if (!this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                kv = new ConcurrentDictionary<byte[], byte[]>(this.byteArrayComparer);
                this.tableUpdates[tableName] = kv;
            }

            kv[keyBytes] = null;

            // If this is a tracked table.
            if (this.trackers != null && this.trackers.TryGetValue(tableName, out IKeyValueStoreTracker tracker))
            {
                // Record the object and its old value.
                tracker.ObjectEvent(obj, KeyValueStoreEvent.ObjectDeleted);
            }
        }

        /// <inheritdoc />
        public void RemoveAllKeys(string tableName)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            // Remove buffered updates to prevent replay during commit.
            this.tableUpdates.TryRemove(tableName, out _);

            // Signal the underlying table to be cleared during commit.
            this.tablesCleared.Add(tableName);
        }

        /// <inheritdoc />
        public void Commit()
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            this.isInTransaction = false;

            this.Repository.OnCommit(this);
            this.tableUpdates.Clear();
            this.tablesCleared = new ConcurrentBag<string>();

            // Having trackers allows us to postpone updating the lookups
            // until after a successful commit.
            this.lookups?.OnCommit(this.trackers);
        }

        /// <inheritdoc />
        public void Rollback()
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            this.isInTransaction = false;

            this.Repository.OnRollback(this);
            this.tableUpdates.Clear();
            this.tablesCleared = new ConcurrentBag<string>();
        }

        // Has Dispose already been called?
        bool disposed = false;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Protected implementation of Dispose pattern.</summary>
        /// <param name="disposing">Indicates whether disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                if (this.mode == KeyValueStoreTransactionMode.ReadWrite && this.isInTransaction)
                    this.Repository.OnRollback(this);

                this.isInTransaction = false;
            }

            this.disposed = true;
        }
    }
}