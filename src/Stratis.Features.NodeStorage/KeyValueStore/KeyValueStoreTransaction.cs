﻿using System;
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

        private KeyValueStoreTable GetTable(string tableName)
        {
            return this.Repository.GetTable(tableName);
        }

        /// <inheritdoc />
        public int Count(string tableName)
        {
            // Count = snapshot_count - deletes + inserts.
            KeyValueStoreTable table = this.GetTable(tableName);

            int count = this.tablesCleared.Contains(tableName) ? 0 : this.repository.Count(this, table);

            // Determine prior existence of updated keys.
            if (!this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
                return count;

            var updateKeys = kv.Keys.ToArray();
            var existed = this.repository.Exists(this, table, updateKeys);
            for (int i = 0; i < updateKeys.Length; i++)
            {
                byte[] key = updateKeys[i];
                if (!existed[i] && kv[key] != null)
                    count++;
                else if (existed[i] && kv[key] == null)
                    count--;
            }

            return count;
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
            return this.ExistsMultiple<TKey>(tableName, new[] { key })[0];
        }

        /// <inheritdoc />
        public bool[] ExistsMultiple<TKey>(string tableName, TKey[] keys)
        {
            byte[][] serKeys = keys.Select(k => this.Serialize(k)).ToArray();
            KeyValueStoreTable table = this.GetTable(tableName);
            bool[] exists = this.tablesCleared.Contains(tableName) ? new bool[keys.Length] : this.repository.Exists(this, table, serKeys);

            if (this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
                for (int i = 0; i < exists.Length; i++)
                    if (kv.TryGetValue(serKeys[i], out byte[] value))
                        exists[i] = value != null;

            return exists;
        }

        /// <inheritdoc />
        public bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj)
        {
            obj = this.SelectMultiple<TKey, TObject>(tableName, new[] { key })[0];

            return obj != null;
        }

        /// <inheritdoc />
        public List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys)
        {
            byte[][] serKeys = keys.Select(k => this.Serialize(k)).ToArray();
            KeyValueStoreTable table = this.GetTable(tableName);

            byte[][] objects = this.tablesCleared.Contains(tableName) ? new byte[keys.Length][] : this.repository.Get(this, table, serKeys);
            if (this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
                for (int i = 0; i < objects.Length; i++)
                    if (kv.TryGetValue(serKeys[i], out byte[] value))
                        objects[i] = value;

            return objects.Select(v => this.Deserialize<TObject>(v)).ToList();
        }

        /// <inheritdoc />
        public Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName)
        {
            return this.SelectForward<TKey, TObject>(tableName).ToDictionary(kv => kv.Item1, kv => kv.Item2);
        }

        /// <inheritdoc />
        public IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName)
        {
            var table = this.GetTable(tableName);

            this.tableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> updates);

            var yielded = new HashSet<byte[]>(this.byteArrayComparer);

            if (!this.tablesCleared.Contains(tableName))
            {
                foreach ((byte[] key, byte[] value) in this.repository.GetAll(this, table))
                {
                    if (updates != null && updates.TryGetValue(key, out byte[] updateValue))
                    {
                        if (updateValue == null)
                            continue;

                        yielded.Add(key);

                        yield return (this.Deserialize<TKey>(key), this.Deserialize<TObject>(updateValue));
                    }

                    if (value == null)
                        continue;

                    yield return (this.Deserialize<TKey>(key), this.Deserialize<TObject>(value));
                }
            }

            if (updates != null)
                foreach (KeyValuePair<byte[], byte[]> kv in updates)
                    if (kv.Value != null && !yielded.Contains(kv.Key))
                        yield return (this.Deserialize<TKey>(kv.Key), this.Deserialize<TObject>(kv.Value));
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