using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStore
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
        private readonly KeyValueStoreRepository repository;

        /// <summary>The mode of the transaction.</summary>
        private readonly KeyValueStoreTransactionMode mode;

        /// <summary>Used to buffer changes to records until a commit takes place.</summary>
        internal ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>> TableUpdates { get; private set; }

        /// <summary>Used to buffer clearing of table contents until a commit takes place.</summary>
        internal ConcurrentBag<string> TablesCleared { get; private set; }

        /// <summary>Comparer used to compare two byte arrays for equality.</summary>
        private IEqualityComparer<byte[]> byteArrayComparer = new ByteArrayComparer();

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
            this.repository = (KeyValueStoreRepository)keyValueStoreRepository;
            this.mode = mode;
            this.TableUpdates = new ConcurrentDictionary<string, ConcurrentDictionary<byte[], byte[]>>();
            this.TablesCleared = new ConcurrentBag<string>();

            keyValueStoreRepository.OnBeginTransaction(this, mode);
            this.isInTransaction = true;
        }

        private KeyValueStoreTable GetTable(string tableName)
        {
            return this.repository.GetTable(tableName);
        }

        /// <inheritdoc />
        public int Count(string tableName)
        {
            // Count = snapshot_count - deletes + inserts.
            KeyValueStoreTable table = this.GetTable(tableName);

            int count = this.TablesCleared.Contains(tableName) ? 0 : this.repository.Count(this, table);

            // Determine prior existence of updated keys.
            if (!this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
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
            return this.repository.Serialize(obj);
        }

        private T Deserialize<T>(byte[] objBytes)
        {
            return this.repository.Deserialize<T>(objBytes);
        }

        /// <inheritdoc />
        public void Insert<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            if (!this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                kv = new ConcurrentDictionary<byte[], byte[]>(this.byteArrayComparer);
                this.TableUpdates[tableName] = kv;
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
            bool[] exists = this.TablesCleared.Contains(tableName) ? new bool[keys.Length] : this.repository.Exists(this, table, serKeys);

            if (this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                for (int i = 0; i < exists.Length; i++)
                {
                    if (kv.TryGetValue(serKeys[i], out byte[] value))
                        exists[i] = value != null;
                }
            }

            return exists;
        }

        /// <inheritdoc />
        public bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj)
        {
            byte[] keyBytes = this.Serialize(key);
            byte[] valueBytes;

            if (!this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv) || !kv.TryGetValue(keyBytes, out valueBytes))
                valueBytes = this.TablesCleared.Contains(tableName) ? null : this.repository.Get(this, this.GetTable(tableName), new[] { keyBytes }).First();

            obj = this.Deserialize<TObject>(valueBytes);

            // Return false if value did not exist.
            return valueBytes != null;
        }

        /// <inheritdoc />
        public List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys)
        {
            byte[][] serKeys = keys.Select(k => this.Serialize(k)).ToArray();
            KeyValueStoreTable table = this.GetTable(tableName);

            byte[][] objects = this.TablesCleared.Contains(tableName) ? new byte[keys.Length][] : this.repository.Get(this, table, serKeys);
            if (this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (serKeys[i] != null && kv.TryGetValue(serKeys[i], out byte[] value))
                        objects[i] = value;
                }
            }

            return objects.Select(v => this.Deserialize<TObject>(v)).ToList();
        }

        /// <inheritdoc />
        public Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName)
        {
            return this.SelectAll<TKey, TObject>(tableName).ToDictionary(kv => kv.Item1, kv => kv.Item2);
        }

        private IEnumerable<O> MergeSortedEnumerations<O, T>(IEnumerable<O> primary, IEnumerable<O> secondary, Func<O, T> keySelector, IComparer<T> comparer, bool descending = false)
        {
            while (true)
            {
                O first = primary.FirstOrDefault();
                O second = secondary.FirstOrDefault();

                if (first == null && second == null)
                    break;

                int cmp = (second == null) ? -1 : ((first != null) ? comparer.Compare(keySelector(first), keySelector(second)) * (descending ? -1 : 1) : 1);

                if (cmp <= 0)
                {
                    yield return first;
                    primary = primary.Skip(1);

                    // Remove if duplicated in secondary.
                    if (cmp == 0)
                        secondary = secondary.Skip(1);
                }
                else
                {
                    yield return second;
                    secondary = secondary.Skip(1);
                }
            }
        }

        private IEnumerable<(TKey, TObject)> SelectAll<TKey, TObject>(string tableName, bool keysOnly = false, bool? backwards = null)
        {
            IEnumerable<(byte[], byte[])> res = null;
            bool ignoreDB = this.TablesCleared.Contains(tableName);

            this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> upd);

            // Not sorted?
            if (backwards == null)
            {
                res = upd?.Where(k => k.Value != null).Select(k => (k.Key, k.Value));

                if (!ignoreDB)
                {
                    var dbRows = this.repository.GetAll(this, this.GetTable(tableName), keysOnly: keysOnly, backwards: backwards ?? false);

                    res = (res == null) ? dbRows : res.Concat(dbRows.Where(k => !upd.ContainsKey(k.Item1)));
                }
            }
            else
            {
                var table = this.GetTable(tableName);
                var byteListComparer = new ByteArrayComparer();
                var updates = (upd == null) ? null : ((bool)backwards ? upd.OrderByDescending(k => k.Key, byteListComparer) : upd.OrderBy(k => k.Key, byteListComparer)).Select(k => (k.Key, keysOnly ? null : k.Value));

                if (!ignoreDB && !this.TablesCleared.Contains(tableName))
                {
                    var dbrows = this.repository.GetAll(this, table, keysOnly: keysOnly, backwards: (bool)backwards);
                    if (updates == null)
                        res = dbrows;
                    else
                        res = this.MergeSortedEnumerations<(byte[], byte[]), byte[]>(dbrows, updates, k => k.Item1, byteListComparer, descending: (bool)backwards);
                }
                else
                {
                    res = updates;
                }
            }

            if (res != null)
            {
                foreach ((byte[] key, byte[] value) in res)
                    yield return (this.Deserialize<TKey>(key), this.Deserialize<TObject>(value));
            }
        }

        /// <inheritdoc />
        public IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName, bool keysOnly = false)
        {
            return this.SelectAll<TKey, TObject>(tableName, backwards: false, keysOnly: keysOnly);
        }

        /// <inheritdoc />
        public IEnumerable<(TKey, TObject)> SelectBackward<TKey, TObject>(string tableName, bool keysOnly = false)
        {
            return this.SelectAll<TKey, TObject>(tableName, backwards: true, keysOnly: keysOnly);
        }

        /// <inheritdoc />
        public void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            var keyBytes = this.Serialize(key);

            if (!this.TableUpdates.TryGetValue(tableName, out ConcurrentDictionary<byte[], byte[]> kv))
            {
                kv = new ConcurrentDictionary<byte[], byte[]>(this.byteArrayComparer);
                this.TableUpdates[tableName] = kv;
            }

            kv[keyBytes] = null;
        }

        /// <inheritdoc />
        public void RemoveAllKeys(string tableName)
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            // Remove buffered updates to prevent replay during commit.
            this.TableUpdates.TryRemove(tableName, out _);

            // Signal the underlying table to be cleared during commit.
            this.TablesCleared.Add(tableName);
        }

        /// <inheritdoc />
        public void Commit()
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            this.isInTransaction = false;

            this.repository.OnCommit(this);

            this.TableUpdates.Clear();
            this.TablesCleared = new ConcurrentBag<string>();
        }

        /// <inheritdoc />
        public void Rollback()
        {
            Guard.Assert(this.mode == KeyValueStoreTransactionMode.ReadWrite);

            this.isInTransaction = false;

            this.repository.OnRollback(this);

            this.TableUpdates.Clear();
            this.TablesCleared = new ConcurrentBag<string>();
        }

        // Has Dispose already been called?
        private bool disposed = false;

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
            if (this.disposed)
                return;

            if (disposing)
            {
                if (this.mode == KeyValueStoreTransactionMode.ReadWrite && this.isInTransaction)
                    this.repository.OnRollback(this);

                this.isInTransaction = false;
            }

            this.disposed = true;
        }
    }
}