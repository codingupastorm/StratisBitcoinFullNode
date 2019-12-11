using System;
using System.Collections.Generic;

namespace Stratis.Features.NodeStorage.Interfaces
{
    /// <summary>
    /// The high-level methods for manipulating values in the key-value store.
    /// </summary>
    public interface IKeyValueStoreTransaction : IDisposable
    {
        void Insert<TKey, TObject>(string tableName, TKey key, TObject obj);
        void InsertMultiple<TKey, TObject>(string tableName, (TKey, TObject)[] objects);
        void InsertDictionary<TKey, TObject>(string tableName, Dictionary<TKey, TObject> objects);
        bool Select<TKey, TObject>(string tableName, TKey key, out TObject obj);
        List<TObject> SelectMultiple<TKey, TObject>(string tableName, TKey[] keys);
        Dictionary<TKey, TObject> SelectDictionary<TKey, TObject>(string tableName);
        IEnumerable<(TKey, TObject)> SelectForward<TKey, TObject>(string tableName);
        void RemoveKey<TKey, TObject>(string tableName, TKey key, TObject obj);
        int Count(string tableName);
        void RemoveAllKeys(string tableName);
        bool Exists<TKey>(string tableName, TKey key);
        bool[] ExistsMultiple<TKey>(string tableName, TKey[] keys);
        void Commit();
        void Rollback();
        string ToString();
    }
}
