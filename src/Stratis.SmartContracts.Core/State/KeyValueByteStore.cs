using System;
using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public class ContractStateTableStore : KeyValueStoreLevelDB
    {
        public ContractStateTableStore(string rootFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IRepositorySerializer repositorySerializer)
            : base(rootFolder, loggerFactory, repositorySerializer)
        {
        }
    }

    /// <summary>
    /// A basic Key/Value store using IKeyValueStore;
    /// </summary>
    public class KeyValueByteStore : ISource<byte[], byte[]>
    {
        protected IKeyValueStore keyValueStore;
        private string table;

        public KeyValueByteStore(IKeyValueStore keyValueStore, string table)
        {
            this.keyValueStore = keyValueStore;
            this.table = table;
        }

        public byte[] Get(byte[] key)
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (!t.Select(this.table, key, out byte[] value))
                    return null;

                return value;
            }
        }

        public void Put(byte[] key, byte[] val)
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                t.Insert(this.table, key, val);
                t.Commit();
            }
        }

        public void Delete(byte[] key)
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                t.RemoveKey(this.table, key, (byte[])null);
                t.Commit();
            }
        }

        public bool Flush()
        {
            throw new NotImplementedException("Can't flush - no underlying DB");
        }

        /// <summary>
        /// Only use for testing at the moment.
        /// </summary>
        public void Empty()
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                t.RemoveAllKeys(this.table);
                t.Commit();
            }
        }
    }

    /// <summary>
    /// Used for dependency injection. A contract state specific implementation of the above class.
    /// </summary>
    public class ContractStateKeyValueStore : KeyValueByteStore, IDisposable
    {
        private bool mustDispose;

        public ContractStateKeyValueStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, RepositorySerializer repositorySerializer)
            : base(new ContractStateTableStore(dataFolder.SmartContractStatePath, loggerFactory, dateTimeProvider, repositorySerializer), "state") { this.mustDispose = true; }

        public void Dispose()
        {
            if (this.mustDispose)
                this.keyValueStore.Dispose();
        }
    }
}
