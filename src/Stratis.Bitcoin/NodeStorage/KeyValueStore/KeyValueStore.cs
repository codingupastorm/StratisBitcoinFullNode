using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.KeyValueStore
{
    /// <summary>
    /// Generic key-value store base-class.
    /// </summary>
    public abstract class KeyValueStore : IKeyValueStore
    {
        public IKeyValueStoreRepository Repository { get; protected set; }

        internal ILoggerFactory LoggerFactory { get; private set; }

        /// <summary>
        /// Creates a key-value store.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        public KeyValueStore(ILoggerFactory loggerFactory)
        {
            this.LoggerFactory = loggerFactory;
        }

        public abstract IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables);

        public string[] GetTables()
        {
            return ((KeyValueStoreRepository)this.Repository).Tables.Select(t => t.Value.TableName).ToArray();
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
        }
    }

    /// <summary>
    /// Generic key-value store template. The template parameter supplies the database type.
    /// </summary>
    /// <typeparam name="R">The database-specific repository class.</typeparam>
    public class KeyValueStore<R> : KeyValueStore where R : KeyValueStoreRepository
    {
        /// <summary>
        /// Creates a key-value store.
        /// </summary>
        /// <param name="rootPath">The location to create the store.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="repositorySerializer">The serializer to use.</param>
        public KeyValueStore(string rootPath, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) :
            base(loggerFactory)
        {
            this.Repository = (R)Activator.CreateInstance(typeof(R), loggerFactory, repositorySerializer);
            this.Repository.Init(rootPath);
        }

        /// <inheritdoc/>
        public override IKeyValueStoreTransaction CreateTransaction(KeyValueStoreTransactionMode mode, params string[] tables)
        {
            return this.Repository.CreateKeyValueStoreTransaction(mode, tables);
        }

        // Flag: Has Dispose already been called?
        private bool disposed = false;

        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                this.Repository.Dispose();
            }

            this.disposed = true;

            // Call the base class implementation.
            base.Dispose(disposing);
        }
    }
}
