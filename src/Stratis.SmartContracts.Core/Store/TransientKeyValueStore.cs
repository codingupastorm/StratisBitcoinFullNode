using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;

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
            : base(dataFolder.TransientStorePath, loggerFactory, repositorySerializer)
        {
        }
    }

    /// <summary>
    /// Implements the transient store operations on top of the ITransientKeyValueStore database
    /// </summary>
    public class TransientStore
    {
        public const string Table = "transient";
        public byte[] MinBlockHeightKey = Encoding.ASCII.GetBytes("MinBlockHeight");

        private readonly ITransientKeyValueStore repository;

        public TransientStore(ITransientKeyValueStore repository)
        {
            this.repository = repository;
        }

        public void Persist(uint256 txId, uint blockHeight, TransientStorePrivateData data)
        {
            var key = new TransientStoreKey(txId.ToBytes(), Guid.NewGuid(), blockHeight);
            var compositePurgeIndexKey = new CompositePurgeIndexKey(blockHeight);

            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                var hasValue = tx.Select(Table, MinBlockHeightKey, out uint minBlockHeight);

                // Update the min block height if necessary.
                if (!hasValue || minBlockHeight > blockHeight)
                {
                    tx.Insert(Table, MinBlockHeightKey, blockHeight);
                }

                tx.Insert(Table, key.ToBytes(), data.ToBytes());
                tx.Insert(Table, compositePurgeIndexKey.ToBytes(), new byte[] {});
                tx.Commit();
            }
        }

        /// <summary>
        /// Returns the lowest block height for the data remaining in the transient store.
        /// </summary>
        /// <returns></returns>
        public uint GetMinBlockHeight()
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.Read, Table))
            {
                return !tx.Select(Table, this.MinBlockHeightKey, out uint minBlockHeight) ? 0 : minBlockHeight;
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
    public struct CompositePurgeIndexKey
    {
        public byte[] PurgeHeightPrefix;
        public uint BlockHeight;

        public CompositePurgeIndexKey(uint blockHeight)
        {
            this.PurgeHeightPrefix = BitConverter.GetBytes('H').Take(1).ToArray();
            this.BlockHeight = blockHeight;
        }

        public byte[] ToBytes()
        {
            var blockHeight = BitConverter.GetBytes(this.BlockHeight);
            var separator = new byte[] {0x00};

            var result = new byte[this.PurgeHeightPrefix.Length + blockHeight.Length + separator.Length];
            Array.Copy(this.PurgeHeightPrefix, result, this.PurgeHeightPrefix.Length);
            Array.Copy(separator, 0, result, this.PurgeHeightPrefix.Length, separator.Length);
            Array.Copy(blockHeight, 0, result, this.PurgeHeightPrefix.Length + separator.Length, blockHeight.Length);

            return result;
        }
    }

    /// <summary>
    /// Contains a representation of private data for storage in the transient store.
    ///
    /// TODO the data represented here may change, but the final representation should always be as a byte array.
    /// </summary>
    public class TransientStorePrivateData
    {
        private readonly byte[] data;

        public TransientStorePrivateData(byte[] data)
        {
            this.data = new byte[data.Length];
            Array.Copy(data, this.data, data.Length);
        }

        public byte[] ToBytes()
        {
            var result = new byte[this.data.Length];
            Array.Copy(this.data, result, this.data.Length);

            return result;
        }
    }
}
