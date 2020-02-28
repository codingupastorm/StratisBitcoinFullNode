using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.SmartContracts.Core.Store
{
    public interface ITransientKeyValueStore : IKeyValueRepositoryStore
    {

    }

    /// <summary>
    /// The underlying levelDB database for the transient store
    /// </summary>
    public class TransientKeyValueStore : KeyValueStoreLevelDB, ITransientKeyValueStore
    {
        public TransientKeyValueStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) 
            : base(loggerFactory, repositorySerializer)
        {

        }
    }

    /// <summary>
    /// Implements the transient store operations on top of the ITransientKeyValueStore database
    /// </summary>
    public class TransientStore
    {
        private const string Table = "transient";

        private readonly ITransientKeyValueStore repository;

        public TransientStore(ITransientKeyValueStore repository)
        {
            this.repository = repository;
        }

        public void Persist(ReadWriteSet rws)
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                tx.Insert(Table, "", "");
                tx.Commit();
            }
        }
    }

    /// <summary>
    /// A composite key comprised of the txid, a uuid and block height.
    /// </summary>
    public struct TransientStoreKey
    {
        public readonly byte[] TxId;

        public readonly Guid UUID;

        public readonly uint BlockHeight;

        public TransientStoreKey(byte[] txId, Guid uuid, uint blockHeight)
        {
            this.TxId = new byte[txId.Length];
            Array.Copy(txId, this.TxId, txId.Length);

            this.UUID = uuid;
            this.BlockHeight = blockHeight;
        }

        public byte[] ToBytes()
        {
            var guid = this.UUID.ToByteArray();
            var result = new byte[this.TxId.Length + guid.Length + sizeof(uint)];
            Array.Copy(this.TxId, result, this.TxId.Length);
            Array.Copy(guid, 0, result, this.TxId.Length, guid.Length);
            Array.Copy(BitConverter.GetBytes(this.BlockHeight), 0, result, this.TxId.Length + guid.Length, sizeof(uint));

            return result;
        }
    }
}
