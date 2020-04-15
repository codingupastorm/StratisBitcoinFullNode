using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.SmartContracts.Core.Store
{
    public interface IMissingPrivateDataStore
    {
        void Add(uint256 txId);
        void Remove(uint256 txId);
        IEnumerable<uint256> GetMissingEntries();
    }

    /// <summary>
    /// Awful name
    /// </summary>
    public class MissingPrivateDataStore : IMissingPrivateDataStore
    {
        public const string Table = "missing";

        private readonly ITransientKeyValueStore repository;

        public MissingPrivateDataStore(ITransientKeyValueStore repository)
        {
            this.repository = repository;
        }

        public void Add(uint256 txId)
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                tx.Insert(Table, txId.ToBytes(),  new byte[0]);
                tx.Commit();
            }
        }

        public void Remove(uint256 txId)
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                tx.RemoveKey(Table, txId.ToBytes(), (object) null);
                tx.Commit();
            }
        }

        public IEnumerable<uint256> GetMissingEntries()
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.Read, Table))
            {
                return tx.SelectAll<byte[], uint>(Table).Select(x => new uint256(x.Item1));
            }
        }
    }
}
