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
using Stratis.Features.Wallet.Tables;

namespace Stratis.Features.Wallet
{
    public class FastWalletRepository : IWalletRepository
    {
        private readonly DataFolder dataFolder;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;

        private readonly ConcurrentDictionary<string, WalletContainer> wallets;

        public Network Network { get; }

        public FastWalletRepository(ILoggerFactory loggerFactory,
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
            // TODO: Lock
        }

        // TODO: This interface does _WAY_ too much.

        public void Initialize(bool dbPerWallet = true)
        {
            // TODO: dbPerWallet?
            foreach (string walletName in GetWalletNamesInDataFolder())
            {
                var walletDatabase = new WalletDatabase(this.dataFolder, walletName);
                var walletContainer = new WalletContainer(walletDatabase); // Also sets up the lookups.

                this.wallets[walletName] = walletContainer;
            }
        }

        private IEnumerable<string> GetWalletNamesInDataFolder()
        {
            return Directory.EnumerateFiles(this.dataFolder.WalletPath, "*.db")
                .Select(p => p.Substring(this.dataFolder.WalletPath.Length + 1).Split('.')[0]);
        }


        public void Shutdown()
        {
            // TODO: Wallet per db ? 

            foreach (WalletContainer walletContainer in this.wallets.Values)
            {
                walletContainer.Database.Dispose();
            }
        }

        public Bitcoin.Features.Wallet.Wallet GetWallet(string walletName)
        {
            // TODO: TryGetValue
            WalletContainer walletContainer = this.wallets[walletName];
            WalletRow wallet = walletContainer.Wallet;

            var res = new Bitcoin.Features.Wallet.Wallet(this)
            {
                Name = walletName,
                EncryptedSeed = wallet.EncryptedSeed,
                ChainCode = (wallet.ChainCode == null) ? null : Convert.FromBase64String(wallet.ChainCode),
                BlockLocator = wallet.BlockLocator.Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(strHash => uint256.Parse(strHash)).ToList(),
                CreationTime = DateTimeOffset.FromUnixTimeSeconds(wallet.CreationTime)
            };

            res.AccountsRoot = new List<AccountRoot>();
            res.AccountsRoot.Add(new AccountRoot(res)
            {
                LastBlockSyncedHeight = wallet.LastBlockSyncedHeight,
                LastBlockSyncedHash = (wallet.LastBlockSyncedHash == null) ? null : new uint256(wallet.LastBlockSyncedHash),
                CoinType = (CoinType)this.Network.Consensus.CoinType
            });

            return res;
        }

        // Could be renamed. Called when a transaction comes through the mempool.
        public void ProcessTransaction(string walletName, Transaction transaction, uint256 txId = null)
        {
            throw new NotImplementedException();
        }

        // Could be renamed. Called when a transaction is removed from the mempool.
        public DateTimeOffset? RemoveUnconfirmedTransaction(string walletName, uint256 txId)
        {
            throw new NotImplementedException();
        }

        // Work out whether we need to store / update anything for this block.
        public void ProcessBlock(Block block, ChainedHeader header, string walletName = null)
        {
            throw new NotImplementedException();
        }

        public void ProcessBlocks(IEnumerable<(ChainedHeader header, Block block)> blocks, string walletName = null)
        {
            foreach ((ChainedHeader header, Block block) blockPackage in blocks)
            {
                this.ProcessBlock(blockPackage.block, blockPackage.header, walletName);
            }
        }

        public Bitcoin.Features.Wallet.Wallet CreateWallet(string walletName, string encryptedSeed, byte[] chainCode, HashHeightPair lastBlockSynced,
            BlockLocator blockLocator, long? creationTime = null)
        {
            throw new NotImplementedException("Not needed for me to test.");
        }

        public bool DeleteWallet(string walletName)
        {
            throw new NotImplementedException("Not needed for me to test.");
        }

        public HdAccount CreateAccount(string walletName, int accountIndex, string accountName, ExtPubKey extPubKey,
            DateTimeOffset? creationTime = null, (int external, int change)? addressCounts = null)
        {
            throw new NotImplementedException("Not needed for me to test.");
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<(HdAddress address, Money confirmed, Money total)> GetUsedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException("Not needed for me to test");
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
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<(uint256 txId, DateTimeOffset creationTime)> RemoveAllUnconfirmedTransactions(string walletName)
        {
            throw new NotImplementedException("Not needed for me to test");
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
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<TransactionData> GetAllTransactions(AddressIdentifier addressIdentifier, int limit = Int32.MaxValue,
            TransactionData prev = null, bool @descending = true, bool includePayments = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<TransactionData> GetTransactionInputs(string walletName, string accountName, DateTimeOffset? transactionTime,
            uint256 transactionId, bool includePayments = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<TransactionData> GetTransactionOutputs(string walletName, string accountName, DateTimeOffset? transactionTime,
            uint256 transactionId, bool includePayments = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName, string accountName = null)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public List<string> GetWalletNames()
        {
            var walletNames = new List<string>();

            foreach (KeyValuePair<string, WalletContainer> wallet in this.wallets)
            {
                walletNames.Add(wallet.Key);
            }

            return walletNames;
        }

        public IWalletAddressReadOnlyLookup GetWalletAddressLookup(string walletName)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IWalletTransactionReadOnlyLookup GetWalletTransactionLookup(string walletName)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public AddressIdentifier GetAddressIdentifier(string walletName, string accountName = null, int? addressType = null,
            int? addressIndex = null)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public IEnumerable<HdAddress> GetAccountAddresses(WalletAccountReference accountReference, int addressType, int count)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public void AddWatchOnlyAddresses(string walletName, string accountName, int addressType, List<HdAddress> addresses, bool force = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public void AddWatchOnlyTransactions(string walletName, string accountName, HdAddress address, ICollection<TransactionData> transactions,
            bool force = false)
        {
            throw new NotImplementedException("Not needed for me to test");
        }

        public bool TestMode { get; set; }
    }
}
