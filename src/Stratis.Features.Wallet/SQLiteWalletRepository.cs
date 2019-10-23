using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.Wallet
{
    public class SQLiteWalletRepository : IWalletRepository
    {
        private readonly DataFolder dataFolder;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;

        private readonly ConcurrentDictionary<string, WalletContainer> wallets;

        public Network Network { get; }

        public SQLiteWalletRepository(ILoggerFactory loggerFactory,
            DataFolder dataFolder,
            Network network,
            IDateTimeProvider dateTimeProvider,
            IScriptAddressReader scriptAddressReader)
        {
            this.Network = network;
            this.dataFolder = dataFolder;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.wallets = new ConcurrentDictionary<string, WalletContainer>();
        }

        public void Initialize(bool dbPerWallet = true)
        {
            // TODO: dbPerWallet?

            // TODO: Method to get wallet name.
            foreach (string walletName in Directory.EnumerateFiles(this.dataFolder.WalletPath, "*.db")
                .Select(p => p.Substring(this.dataFolder.WalletPath.Length + 1).Split('.')[0]))
            {
                var walletDatabase = new WalletDatabase(this.dataFolder, walletName);
                var walletContainer = new WalletContainer
                {
                    Database = walletDatabase,
                    Wallet = 
                };
                var conn = GetConnection(walletName);

                HDWallet wallet = conn.GetWalletByName(walletName);
                var walletContainer = new WalletContainer(conn, wallet, new ProcessBlocksInfo(conn, null, wallet));
                this.Wallets[walletName] = walletContainer;

                walletContainer.AddressesOfInterest.AddAll(wallet.WalletId);
                walletContainer.TransactionsOfInterest.AddAll(wallet.WalletId);

                this.logger.LogDebug("Added '{0}` to wallet collection.", wallet.Name);
            }


        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        public Bitcoin.Features.Wallet.Wallet GetWallet(string walletName)
        {
            throw new NotImplementedException();
        }

        public void ProcessTransaction(string walletName, Transaction transaction, uint256 txId = null)
        {
            throw new NotImplementedException();
        }

        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            throw new NotImplementedException();
        }

        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            throw new NotImplementedException();
        }

        public Bitcoin.Features.Wallet.Wallet CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, HashHeightPair lastBlockSynced,
            BlockLocator blockLocator, long? creationTime = null)
        {
            throw new NotImplementedException();
        }

        public bool DeleteWallet(string walletName)
        {
            throw new NotImplementedException();
        }

        public HdAccount CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey,
            DateTimeOffset? creationTime = null, (int external, int change)? addressCounts = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<(HdAddress address, Money confirmed, Money total)> GetUsedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference accountReference, int currentChainHeight,
            int confirmations = 0, int? coinBaseMaturity = null)
        {
            throw new NotImplementedException();
        }

        public (Money totalAmount, Money confirmedAmount, Money spendableAmount) GetAccountBalance(
            WalletAccountReference walletAccountReference, int currentChainHeight, int confirmations = 0,
            int? coinBaseMaturity = null, (int, int)? address = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset? RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<(uint256 txId, DateTimeOffset creationTime)> RemoveAllUnconfirmedTransactions(string walletName)
        {
            throw new NotImplementedException();
        }

        public ChainedHeader FindFork(string walletName, ChainedHeader chainTip)
        {
            throw new NotImplementedException();
        }

        public (bool, IEnumerable<(uint256, DateTimeOffset)>) RewindWallet(string walletName, ChainedHeader lastBlockSynced)
        {
            throw new NotImplementedException();
        }

        public ITransactionContext BeginTransaction(string walletName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TransactionData> GetAllTransactions(AddressIdentifier addressIdentifier, int limit = Int32.MaxValue,
            TransactionData prev = null, bool @descending = true, bool includePayments = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TransactionData> GetTransactionInputs(string walletName, string accountName, DateTimeOffset? transactionTime,
            uint256 transactionId, bool includePayments = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<TransactionData> GetTransactionOutputs(string walletName, string accountName, DateTimeOffset? transactionTime,
            uint256 transactionId, bool includePayments = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName, string accountName = null)
        {
            throw new NotImplementedException();
        }

        public List<string> GetWalletNames()
        {
            throw new NotImplementedException();
        }

        public IWalletAddressReadOnlyLookup GetWalletAddressLookup(string walletName)
        {
            throw new NotImplementedException();
        }

        public IWalletTransactionReadOnlyLookup GetWalletTransactionLookup(string walletName)
        {
            throw new NotImplementedException();
        }

        public AddressIdentifier GetAddressIdentifier(string walletName, string accountName = null, int? addressType = null,
            int? addressIndex = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetAccountAddresses(WalletAccountReference accountReference, int addressType, int count)
        {
            throw new NotImplementedException();
        }

        public void AddWatchOnlyAddresses(string walletName, string accountName, int addressType, List<HdAddress> addresses, bool force = false)
        {
            throw new NotImplementedException();
        }

        public void AddWatchOnlyTransactions(string walletName, string accountName, HdAddress address, ICollection<TransactionData> transactions,
            bool force = false)
        {
            throw new NotImplementedException();
        }

        public bool TestMode { get; set; }
    }
}
