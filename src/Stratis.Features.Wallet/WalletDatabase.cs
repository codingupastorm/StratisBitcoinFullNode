using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Wallet.Tables;

namespace Stratis.Features.Wallet
{
    /// <summary>
    /// Controls direct access to the database on-disk always.
    /// </summary>
    public class WalletDatabase : IDisposable
    {
        private readonly SQLiteConnection db;

        public WalletDatabase(DataFolder dataFolder, string walletName)
        {
            var databasePath = Path.Combine(dataFolder.WalletPath, $"{walletName}.db");

            // Open connection and create tables if they don't already exist.
            this.db = new SQLiteConnection(databasePath);
            this.db.CreateTable<WalletRow>();
            this.db.CreateTable<AddressRow>();
            this.db.CreateTable<TransactionDataRow>();
        }

        // TODO: This is assuming that a wallet always has its own db.

        public void InsertWallet(WalletRow wallet)
        {
            this.db.Insert(wallet);
        }

        public WalletRow GetWallet()
        {
            // name is only incoming for when db is shared. Unnecessary?
            return this.db.Table<WalletRow>().First();
        }

        public IEnumerable<AddressRow> GetAllAddresses()
        {
            // TODO: May need parameters for this later.
            return this.db.Table<AddressRow>();
        }

        public void UpdateOutputToBeSpent()
        {
            throw new NotImplementedException();
        }

        public void AddSpendableOutput()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TransactionDataRow> GetAllUnspentTransactions()
        {
            // TODO: May need parameters for this later.
            return this.db.Table<TransactionDataRow>()
                .Where(x => x.SpendBlockHash == null);
        }

        //public void InsertTransactionData(TransactionDataRow transactionData)
        //{
        //    this.db.Insert(transactionData);
        //}

        //// TODO: This is a test method, not legit.
        //public IEnumerable<TransactionDataRow> GetAllSpendableTransactions()
        //{
        //    var bytes = new byte[] {1, 2, 3};

        //    return this.db.Table<TransactionDataRow>()
        //        .Where(x => x.SpendTxId == null)
        //        .Where(x=>x.OutputBlockHash == bytes);
        //}



        public void Dispose()
        {
            this.db.Dispose();
        }
    }
}
