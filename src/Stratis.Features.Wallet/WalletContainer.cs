using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.Wallet.Tables;
using Stratis.Features.Wallet.Utils;

namespace Stratis.Features.Wallet
{
    public class WalletContainer
    {
        public WalletDatabase Database { get; }
        public WalletRow Wallet { get; }

        // TODO: It's actually ScriptPubKeys
        public Dictionary<byte[], AddressRow> AddressesOfInterest { get; }

        // TODO: It's actually UTXOs
        public HashSet<byte[]> TransactionsOfInterest { get; }

        public WalletContainer(WalletDatabase database)
        {
            this.Database = database;
            this.Wallet = this.Database.GetWallet();
            this.AddressesOfInterest = new Dictionary<byte[], AddressRow>(new ByteArrayEqualityComparer());
            this.TransactionsOfInterest = new HashSet<byte[]>(new ByteArrayEqualityComparer());

            IEnumerable<AddressRow> addresses = this.Database.GetAllAddresses();

            foreach (AddressRow address in addresses)
            {
                this.AddressesOfInterest.Add(address.ScriptPubKey, address);
            }

            IEnumerable<TransactionDataRow> txs = this.Database.GetAllUnspentTransactions();

            foreach (TransactionDataRow tx in txs)
            {
                this.TransactionsOfInterest.Add(new OutPoint(new uint256(tx.OutputTxId), tx.OutputIndex).ToBytes());
            }
        }
    }
}
