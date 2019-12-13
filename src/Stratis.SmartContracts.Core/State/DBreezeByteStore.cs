using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.KeyValueStoreDBreeze;
using Stratis.Bitcoin.Utilities;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public class ContractStateTableStore : KeyValueStore<KeyValueStoreDBreeze>
    {
        public ContractStateTableStore(string rootFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IRepositorySerializer repositorySerializer)
            : base(rootFolder, loggerFactory, dateTimeProvider, repositorySerializer)
        {
        }
    }

    /// <summary>
    /// A basic Key/Value store using IKeyValueStore;
    /// </summary>
    public class DBreezeByteStore : ISource<byte[], byte[]>
    {
        private IKeyValueStore keyValueStore;
        private string table;

        public DBreezeByteStore(IKeyValueStore keyValueStore, string table)
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
    public class DBreezeContractStateStore : DBreezeByteStore
    {
        public DBreezeContractStateStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, DBreezeSerializer repositorySerializer)
            : base(new ContractStateTableStore(dataFolder.SmartContractStatePath, loggerFactory, dateTimeProvider, repositorySerializer), "state") { }
    }
}
