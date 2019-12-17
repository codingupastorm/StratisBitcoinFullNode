using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mining;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class TokenlessFeatureRegistration
    {
        public static IFullNodeBuilder AsTokenlessNetwork(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<TokenlessFeature>()
                    .FeatureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IWalletManager, TokenlessWalletManagerStub>());

                        services.Replace(ServiceDescriptor.Singleton<ITxMempool, TokenlessMempool>());
                        services.Replace(ServiceDescriptor.Singleton<IMempoolValidator, TokenlessMempoolValidator>());
                        services.AddSingleton<BlockDefinition, TokenlessBlockDefinition>();
                        services.AddSingleton<ITokenlessSigner, TokenlessSigner>();
                        services.AddSingleton<ICoreComponent, CoreComponent>();

                        // In place of wallet.
                        services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                    });
            });

            return fullNodeBuilder;
        }

        public static IFullNodeBuilder UseTokenlessPoaConsenus(this IFullNodeBuilder fullNodeBuilder, Network network)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ConsensusFeature>("consensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        // Base
                        services.AddSingleton<DBCoinView>();
                        services.AddSingleton<IDBCoinViewStore, DBCoinViewStore>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<IConsensusRuleEngine, TokenlessConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());

                        // PoA Specific
                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, PoAMiner>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<PoAMinerSettings>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();

                        // Smart Contract Specific
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();

                        // Permissioned membership.
                        services.AddSingleton<CertificatesManager>();
                        services.AddSingleton<RevocationChecker>();

                        var options = (PoAConsensusOptions)network.Consensus.Options;
                        if (options.EnablePermissionedMembership)
                        {
                            ServiceDescriptor descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(INetworkPeerFactory));
                            services.Remove(descriptor);
                            services.AddSingleton<INetworkPeerFactory, TlsEnabledNetworkPeerFactory>();
                        }
                    });
            });

            return fullNodeBuilder;
        }
    }

    public sealed class TokenlessWalletManagerStub : IWalletManager
    {
        public uint256 WalletTipHash => throw new NotImplementedException();

        public int WalletTipHeight => throw new NotImplementedException();

        public bool ContainsWallets => throw new NotImplementedException();

        public IWalletRepository WalletRepository => throw new NotImplementedException();

        public (Wallet, Mnemonic) CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null)
        {
            throw new NotImplementedException();
        }

        public void DeleteWallet(string walletName)
        {
            throw new NotImplementedException();
        }

        public ChainedHeader FindFork(string walletName, ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            throw new NotImplementedException();
        }

        public AddressBalance GetAddressBalance(string address)
        {
            throw new NotImplementedException();
        }

        public int GetAddressBufferSize()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IEnumerable<string>> GetAddressGroupings(string walletName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public ExtKey GetExtKey(WalletAccountReference accountReference, string password = "")
        {
            throw new NotImplementedException();
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            throw new NotImplementedException();
        }

        public AccountHistory GetHistory(HdAccount account)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetUnspentTransactionsInWallet(string walletName, int confirmations, Func<HdAccount, bool> accountFilter)
        {
            throw new NotImplementedException();
        }

        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            throw new NotImplementedException();
        }

        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
            throw new NotImplementedException();
        }

        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<(HdAddress address, Money confirmed, Money total)> GetUsedAddresses(WalletAccountReference accountReference, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            throw new NotImplementedException();
        }

        public Wallet GetWallet(string walletName)
        {
            throw new NotImplementedException();
        }

        public string GetWalletFileExtension()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetWalletsNames()
        {
            throw new NotImplementedException();
        }

        public int LastBlockHeight()
        {
            throw new NotImplementedException();
        }

        public Wallet LoadWallet(string password, string name)
        {
            throw new NotImplementedException();
        }

        public void LockWallet(string name)
        {
            throw new NotImplementedException();
        }

        public void ProcessBlock(Block block, ChainedHeader chainedHeader = null)
        {
            throw new NotImplementedException();
        }

        public void ProcessBlocks(Func<int, IEnumerable<(ChainedHeader, Block)>> blockProvider)
        {
            throw new NotImplementedException();
        }

        public bool ProcessTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null, ChainedHeader lastBlockSynced = null)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime, ChainedHeader lastBlockSynced = null)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            throw new NotImplementedException();
        }

        public void RemoveBlocks(ChainedHeader fork)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            throw new NotImplementedException();
        }

        public void RemoveUnconfirmedTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public void RewindWallet(string walletName, ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public void SaveWallet(string wallet)
        {
            throw new NotImplementedException();
        }

        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void UnlockWallet(string password, string name, int timeout)
        {
            throw new NotImplementedException();
        }

        public void UpdateLastBlockSyncedHeight(ChainedHeader tip, string walletName = null)
        {
            throw new NotImplementedException();
        }

        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            throw new NotImplementedException();
        }

        public ChainedHeader WalletCommonTip(ChainedHeader consensusTip)
        {
            throw new NotImplementedException();
        }
    }
}
