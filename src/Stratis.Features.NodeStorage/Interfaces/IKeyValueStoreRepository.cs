using System.Collections.Generic;

namespace Stratis.Features.NodeStorage.Interfaces
{
    /// <summary>
    /// Represents a glue-layer containing the basic methods that all key-value databases should support.
    /// </summary>
    public interface IKeyValueStoreRepository
    {
        /// <summary>
        /// Initialize the underlying database / glue-layer.
        /// </summary>
        /// <param name="rootPath">The location of the key-value store.</param>
        void Init(string rootPath);

        /// <summary>
        /// Request the underlying database to start a transaction.
        /// </summary>
        /// <param name="mode">The transaction mode.</param>
        /// <param name="tables">The tables that will be modified.</param>
        /// <returns>A transaction that can be passed as a parameter to the rest of the class methods.</returns>
        IKeyValueStoreTransaction CreateKeyValueStoreTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        /// <summary>
        /// Determines if the keys (array of byte array) exists in the given table.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        /// <param name="keyValueStoreTable">The table to read.</param>
        /// <param name="keys">The keys (byte arrays) to check for existence.</param>
        /// <returns>A boolean array corresponding to each key indicating whether the key exists.</returns>
        bool[] Exists(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable, byte[][] keys);

        int Count(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable);

        /// <summary>
        /// Gets the value (byte array) associated with a key (byte array) in the given table.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        /// <param name="keyValueStoreTable">The table to read.</param>
        /// <param name="keys">The keys (byte arrays) of the values to read.</param>
        /// <returns>The values as an array of byte arrays.</returns>
        byte[][] Get(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable, byte[][] keys);

        /// <summary>
        /// Gets the values (byte arrays) and keys (byte arrays) associated with a table.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        /// <param name="keyValueStoreTable">The table to read.</param>
        /// <param name="keysOnly">Set to <c>true</c> if values are optional.</param>
        /// <returns>The keys and values as byte arrays.</returns>
        IEnumerable<(byte[], byte[])> GetAll(IKeyValueStoreTransaction keyValueStoreTransaction, IKeyValueStoreTable keyValueStoreTable, bool keysOnly = false);

        /// <summary>
        /// A call-back indicating that the transaction is starting.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        /// <param name="mode">The transaction mode.</param>
        void OnBeginTransaction(IKeyValueStoreTransaction keyValueStoreTransaction, KeyValueStoreTransactionMode mode);

        /// <summary>
        /// A call-back indicating that the transaction is being committed.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        void OnCommit(IKeyValueStoreTransaction keyValueStoreTransaction);

        /// <summary>
        /// A call-back indicating that the transaction is ending without being committed.
        /// </summary>
        /// <param name="keyValueStoreTransaction">The transaction.</param>
        void OnRollback(IKeyValueStoreTransaction keyValueStoreTransaction);

        /// <summary>
        /// Gets an object representing a table with the given name.
        /// </summary>
        /// <param name="tableName">The name of the table.</param>
        /// <returns>The object representing the table.</returns>
        IKeyValueStoreTable GetTable(string tableName);

        /// <summary>
        /// Called when the repository is being closed.
        /// </summary>
        void Close();
    }
}
