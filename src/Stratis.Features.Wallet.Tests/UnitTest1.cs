using System;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Wallet.Tables;
using Xunit;

namespace Stratis.Features.Wallet.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void TestMethod1()
        {
            // TODO: Don't use MyDocs lol'
            var dataFolder = new DataFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

            var walletDb = new WalletDatabase(dataFolder, "wallet");

            var transactionData = new TransactionDataDto
            {
                AccountIndex = 0,
                Address = "Address",
                AddressIndex = 0,
                AddressType = 69,
                Value = 0.69m,
                OutputBlockHash = new byte[] {1,2,3}
            };

            walletDb.InsertTransactionData(transactionData);

            var allTxDatas = walletDb.GetAllSpendableTransactions();
        }
    }
}
