using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.NodeStorage.Interfaces
{
    /// <summary>Supported transaction modes.</summary>
    public enum KeyValueStoreTransactionMode
    {
        Read,
        ReadWrite
    }

    /// <summary>
    /// Primary interface methods of a key-value store.
    /// </summary>
    public interface IKeyValueStore : IDisposable
    {
        /// <summary>
        /// The transaction factory for this key-store type.
        /// </summary>
        /// <param name="mode">The transaction mode.</param>
        /// <param name="tables">The tables that will be updated if <paramref name="mode"/> is <see cref="KeyValueStoreTransactionMode.ReadWrite>.</param>
        /// <returns>A transaction specific to the key-store type.</returns>
        IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        /// <summary>
        /// Used to specify trackers to track changes.
        /// </summary>
        /// <param name="keyValueStoreTrackers">The trackers to use.</param>
        void SetLookups(IKeyValueStoreTrackers keyValueStoreTrackers);

        /// <summary>Interface providing control over the updating of transient lookups.</summary>
        IKeyValueStoreTrackers Lookups { get; }

        /// <summary>Interface providing serialization of database objects.</summary>
        IRepositorySerializer RepositorySerializer { get; }

        /// <summary>Interface providing logger factory.</summary>
        ILoggerFactory LoggerFactory { get; }
    }
}