using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.SmartContracts.Core.Receipts
{
    public class PersistentReceiptRepository : IReceiptRepository
    {
        internal const string TableName = "receipts";
        private readonly IReceiptKVStore keyValueStore;

        public PersistentReceiptRepository(IReceiptKVStore persistentReceiptKVStore)
        {
            Guard.NotNull(persistentReceiptKVStore, nameof(persistentReceiptKVStore));

            this.keyValueStore = persistentReceiptKVStore;
        }

        // TODO: Handle pruning old data in case of reorg.

        /// <inheritdoc />
        public void Store(IEnumerable<Receipt> receipts)
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, TableName))
            {
                foreach (Receipt receipt in receipts)
                {
                    t.Insert<uint256, byte[]>(TableName, receipt.TransactionHash, receipt.ToStorageBytesRlp());
                }
                t.Commit();
            }
        }

        /// <inheritdoc />
        public Receipt Retrieve(uint256 hash)
        {
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                return this.GetReceipt(t, hash);
            }
        }

        /// <inheritdoc />
        public IList<Receipt> RetrieveMany(IList<uint256> hashes)
        {
            List<Receipt> ret = new List<Receipt>();
            using (IKeyValueStoreTransaction t = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                foreach (uint256 hash in hashes)
                {
                    ret.Add(this.GetReceipt(t, hash));
                }

                return ret;
            }
        }

        private Receipt GetReceipt(IKeyValueStoreTransaction t, uint256 hash)
        {
            if (!t.Select<uint256, byte[]>(TableName, hash, out byte[] result))
                return null;

            return Receipt.FromStorageBytesRlp(result);
        }
    }
}
