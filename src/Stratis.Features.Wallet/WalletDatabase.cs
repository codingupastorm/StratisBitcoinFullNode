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
    public class WalletDatabase
    {
        private readonly SQLiteConnection db;

        public WalletDatabase(DataFolder dataFolder, string walletName)
        {
            var databasePath = Path.Combine(dataFolder.WalletPath, $"{walletName}.db");

            // Open connection and create tables if they don't already exist.
            this.db = new SQLiteConnection(databasePath);
            this.db.CreateTable<TransactionDataDto>();
            this.db.CreateTable<WalletDto>();
        }

        public WalletDto GetWalletByName(string name)
        {
            // name is only incoming for when db is shared. Unnecessary?
            throw new NotImplementedException();
        }

        public void InsertTransactionData(TransactionDataDto transactionData)
        {
            this.db.Insert(transactionData);
        }

        // TODO: This is a test nmethod, not legit.
        public IEnumerable<TransactionDataDto> GetAllSpendableTransactions()
        {
            var bytes = new byte[] {1, 2, 3};

            return this.db.Table<TransactionDataDto>()
                .Where(x => x.SpendTxId == null)
                .Where(x=>x.OutputBlockHash == bytes);
        }
    }
}
