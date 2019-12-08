using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;

namespace Stratis.Features.NodeStorage.KeyValueStore
{
    /// <summary>
    /// Generic key-value store template. The template parameter supplies the database type. 
    /// </summary>
    /// <typeparam name="R">The database-specific repository class.</typeparam>
    public class KeyValueStore<R> : IKeyValueStore where R : IKeyValueStoreRepository
    {
        internal IDateTimeProvider DateTimeProvider { get; private set; }
        internal IKeyValueStoreRepository Repository { get; private set; }
        internal string RootPath { get; private set; }

        /// <inheritdoc/>
        public IKeyValueStoreTrackers Lookups { get; private set; }

        /// <inheritdoc/>
        public ILoggerFactory LoggerFactory { get; private set; }

        /// <inheritdoc/>
        public IRepositorySerializer RepositorySerializer { get; private set; }

        /// <summary>
        /// Creates a key-value store.
        /// </summary>
        /// <param name="rootPath">The location to create the store.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="dateTimeProvider">The datetime provider.</param>
        /// <param name="repositorySerializer">The serializer to use.</param>
        public KeyValueStore(string rootPath, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IRepositorySerializer repositorySerializer)
        {
            this.RootPath = rootPath;
            this.LoggerFactory = loggerFactory;
            this.DateTimeProvider = dateTimeProvider;
            this.RepositorySerializer = repositorySerializer;

            this.Repository = (R)System.Activator.CreateInstance(typeof(R), this);
            this.Repository.Init(this.RootPath);
        }

        /// <inheritdoc/>
        public void SetLookups(IKeyValueStoreTrackers keyValueStoreTrackers)
        {
            this.Lookups = keyValueStoreTrackers;
        }

        /// <inheritdoc/>
        public IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return this.Repository.CreateTransaction(mode, tables);
        }

        /// <summary>
        /// Disposes of the key-value store.
        /// </summary>
        public void Dispose()
        {
            this.Repository.Close();
        }
    }
}
