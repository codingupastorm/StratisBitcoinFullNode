using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;

namespace Stratis.Features.NodeStorage.KeyValueStore
{
    /// <summary>
    /// Generic key-value store base-class.
    /// </summary>
    public abstract class KeyValueStore : IKeyValueStore
    {
        public readonly IDateTimeProvider DateTimeProvider;
        public readonly string RootPath;
        public readonly ILoggerFactory LoggerFactory;
        public readonly IRepositorySerializer RepositorySerializer;

        public IKeyValueStoreTrackers Lookups { get; private set; }

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
            this.Lookups = null;
        }

        public abstract IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        /// <inheritdoc/>
        public void SetLookups(IKeyValueStoreTrackers keyValueStoreTrackers)
        {
            this.Lookups = keyValueStoreTrackers;
        }

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
        }
    }

    /// <summary>
    /// Generic key-value store template. The template parameter supplies the database type.
    /// </summary>
    /// <typeparam name="R">The database-specific repository class.</typeparam>
    public class KeyValueStore<R> : KeyValueStore where R : KeyValueStoreRepository
    {
        public IKeyValueStoreRepository Repository { get; private set; }

        /// <summary>
        /// Creates a key-value store.
        /// </summary>
        /// <param name="rootPath">The location to create the store.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="dateTimeProvider">The datetime provider.</param>
        /// <param name="repositorySerializer">The serializer to use.</param>
        public KeyValueStore(string rootPath, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IRepositorySerializer repositorySerializer) :
            base(rootPath, loggerFactory, dateTimeProvider, repositorySerializer)
        {
            this.Repository = (R)Activator.CreateInstance(typeof(R), (KeyValueStore)this);
            this.Repository.Init(this.RootPath);
        }

        /// <inheritdoc/>
        public override IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return this.Repository.CreateKeyValueStoreTransaction(mode, tables);
        }

        // Flag: Has Dispose already been called?
        bool disposed = false;

        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                this.Repository.Close();
            }

            this.disposed = true;

            // Call the base class implementation.
            base.Dispose(disposing);
        }
    }
}
