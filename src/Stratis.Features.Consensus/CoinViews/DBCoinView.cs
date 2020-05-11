﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Features.Consensus.CoinViews
{
    /// <summary>
    /// Persistent implementation of coinview using dBreeze database.
    /// </summary>
    public class DBCoinView : ICoinView, IDisposable
    {
        /// <summary>Database key under which the block hash of the coin view's current tip is stored.</summary>
        private static readonly byte[] blockHashKey = new byte[0];

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Hash of the block which is currently the tip of the coinview.</summary>
        private uint256 blockHash;

        /// <summary>Performance counter to measure performance of the database insert and query operations.</summary>
        private readonly BackendPerformanceCounter performanceCounter;

        private BackendPerformanceSnapshot latestPerformanceSnapShot;

        /// <summary>Access to dBreeze database.</summary>
        private readonly IDBCoinViewStore keyValueStore;

        private RepositorySerializer repositorySerializer;

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="dBCoinViewStore">The coinview database.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        /// <param name="nodeStats"></param>
        /// <param name="dBreezeSerializer">The serializer to use for <see cref="IBitcoinSerializable"/> objects.</param>
        public DBCoinView(Network network, IDBCoinViewStore dBCoinViewStore, IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory, INodeStats nodeStats, RepositorySerializer dBreezeSerializer)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dBCoinViewStore, nameof(dBCoinViewStore));

            this.repositorySerializer = dBreezeSerializer;

            // Create the coinview folder if it does not exist.
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.keyValueStore = dBCoinViewStore;
            this.network = network;
            this.performanceCounter = new BackendPerformanceCounter(dateTimeProvider);

            nodeStats.RegisterStats(this.AddBenchStats, StatsType.Benchmark, this.GetType().Name, 400);
        }

        /// <summary>
        /// Initializes the database tables used by the coinview.
        /// </summary>
        public void Initialize()
        {
            Block genesis = this.network.GetGenesis();

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "BlockHash"))
            {
                if (this.GetTipHash(transaction) == null)
                {
                    this.SetBlockHash(transaction, genesis.GetHash());

                    // Genesis coin is unspendable so do not add the coins.
                    transaction.Commit();
                }
            }
        }

        /// <inheritdoc />
        public uint256 GetTipHash(CancellationToken cancellationToken = default(CancellationToken))
        {
            uint256 tipHash;

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.Read))
            {
                tipHash = this.GetTipHash(transaction);
            }

            return tipHash;
        }

        /// <inheritdoc />
        public FetchCoinsResponse FetchCoins(uint256[] txIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            FetchCoinsResponse res = null;
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "BlockHash", "Coins"))
            {
                using (new StopwatchDisposable(o => this.performanceCounter.AddQueryTime(o)))
                {
                    uint256 blockHash = this.GetTipHash(transaction);
                    var result = new UnspentOutputs[txIds.Length];
                    this.performanceCounter.AddQueriedEntities(txIds.Length);

                    int i = 0;
                    foreach (uint256 input in txIds)
                    {
                        UnspentOutputs outputs = null;

                        // The legacy behavior is to reverse the key bytes. Will retain that for now...
                        if (transaction.Select<byte[], Coins>("Coins", input.ToBytes(false), out Coins coins))
                            outputs = new UnspentOutputs(input, coins);

                        this.logger.LogDebug("Outputs for '{0}' were {1}.", input, outputs == null ? "NOT loaded" : "loaded");

                        result[i++] = outputs;
                    }

                    res = new FetchCoinsResponse(result, blockHash);
                }
            }

            return res;
        }

        /// <summary>
        /// Obtains a block header hash of the coinview's current tip.
        /// </summary>
        /// <param name="transaction">Open dBreeze transaction.</param>
        /// <returns>Block header hash of the coinview's current tip.</returns>
        private uint256 GetTipHash(IKeyValueStoreTransaction transaction)
        {
            if (this.blockHash == null && transaction.Select<byte[], uint256>("BlockHash", blockHashKey, out uint256 blockHash))
                this.blockHash = blockHash;

            return this.blockHash;
        }

        /// <summary>
        /// Set's the tip of the coinview to a new block hash.
        /// </summary>
        /// <param name="transaction">Open dBreeze transaction.</param>
        /// <param name="nextBlockHash">Hash of the block to become the new tip.</param>
        private void SetBlockHash(IKeyValueStoreTransaction transaction, uint256 nextBlockHash)
        {
            this.blockHash = nextBlockHash;
            transaction.Insert("BlockHash", blockHashKey, nextBlockHash);
        }

        /// <inheritdoc />
        public void SaveChanges(IList<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash, int height, List<RewindData> rewindDataList = null)
        {
            int insertedEntities = 0;

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "BlockHash", "Coins", "Rewind"))
            {
                using (new StopwatchDisposable(o => this.performanceCounter.AddInsertTime(o)))
                {
                    uint256 current = this.GetTipHash(transaction);
                    if (current != oldBlockHash)
                    {
                        this.logger.LogTrace("(-)[BLOCKHASH_MISMATCH]");
                        throw new InvalidOperationException("Invalid oldBlockHash");
                    }

                    this.SetBlockHash(transaction, nextBlockHash);

                    // Here we'll add items to be inserted in a second pass.
                    List<UnspentOutputs> toInsert = new List<UnspentOutputs>();

                    foreach (var coin in unspentOutputs.OrderBy(utxo => utxo.TransactionId, new UInt256Comparer()))
                    {
                        if (coin.IsPrunable)
                        {
                            this.logger.LogDebug("Outputs of transaction ID '{0}' are prunable and will be removed from the database.", coin.TransactionId);
                            transaction.RemoveKey("Coins", coin.TransactionId.ToBytes(false), coin.ToCoins());
                        }
                        else
                        {
                            // Add the item to another list that will be used in the second pass.
                            // This is for performance reasons: dBreeze is optimized to run the same kind of operations, sorted.
                            toInsert.Add(coin);
                        }
                    }

                    for (int i = 0; i < toInsert.Count; i++)
                    {
                        var coin = toInsert[i];
                        this.logger.LogDebug("Outputs of transaction ID '{0}' are NOT PRUNABLE and will be inserted into the database. {1}/{2}.", coin.TransactionId, i, toInsert.Count);

                        transaction.Insert("Coins", coin.TransactionId.ToBytes(false), this.repositorySerializer.Serialize(coin.ToCoins()));
                    }

                    if (rewindDataList != null)
                    {
                        int nextRewindIndex = this.GetRewindIndex(transaction) + 1;
                        foreach (RewindData rewindData in rewindDataList)
                        {
                            this.logger.LogDebug("Rewind state #{0} created.", nextRewindIndex);

                            transaction.Insert("Rewind", nextRewindIndex, this.repositorySerializer.Serialize(rewindData));
                            nextRewindIndex++;
                        }
                    }

                    insertedEntities += unspentOutputs.Count;
                    transaction.Commit();
                }
            }

            this.performanceCounter.AddInsertedEntities(insertedEntities);
        }

        /// <summary>
        /// Obtains order number of the last saved rewind state in the database.
        /// </summary>
        /// <param name="transaction">Open transaction.</param>
        /// <returns>Order number of the last saved rewind state, or <c>0</c> if no rewind state is found in the database.</returns>
        /// <remarks>TODO: Using <c>0</c> is hacky here, and <see cref="SaveChanges"/> exploits that in a way that if no such rewind data exist
        /// the order number of the first rewind data is 0 + 1 = 1.</remarks>
        private int GetRewindIndex(IKeyValueStoreTransaction transaction)
        {
            (int Key, byte[] Value) firstRow = transaction.SelectAll<int, byte[]>("Rewind", keysOnly: true, SortOrder.Descending).FirstOrDefault();

            return firstRow != default ? firstRow.Key : 0;
        }

        public RewindData GetRewindData(int height)
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                transaction.Select("Rewind", height, out RewindData rewindData);

                return rewindData;
            }
        }

        /// <inheritdoc />
        public uint256 Rewind()
        {
            uint256 res = null;
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "BlockHash", "Coins", "Rewind"))
            {
                if (this.GetRewindIndex(transaction) == 0)
                {
                    transaction.RemoveAllKeys("Coins");
                    this.SetBlockHash(transaction, this.network.GenesisHash);

                    res = this.network.GenesisHash;
                }
                else
                {
                    (int Key, byte[] Value) firstRow = transaction.SelectAll<int, byte[]>("Rewind", sortOrder: SortOrder.Descending).FirstOrDefault();
                    transaction.RemoveKey("Rewind", firstRow.Key, firstRow.Value);
                    var rewindData = this.repositorySerializer.Deserialize<RewindData>(firstRow.Value);
                    this.SetBlockHash(transaction, rewindData.PreviousBlockHash);

                    foreach (uint256 txId in rewindData.TransactionsToRemove)
                    {
                        this.logger.LogDebug("Outputs of transaction ID '{0}' will be removed.", txId);
                        transaction.RemoveKey("Coins", txId.ToBytes(false), (byte[])null);
                    }

                    foreach (UnspentOutputs coin in rewindData.OutputsToRestore)
                    {
                        this.logger.LogDebug("Outputs of transaction ID '{0}' will be restored.", coin.TransactionId);
                        transaction.Insert("Coins", coin.TransactionId.ToBytes(false), this.repositorySerializer.Serialize(coin.ToCoins()));
                    }

                    res = rewindData.PreviousBlockHash;
                }

                transaction.Commit();
            }

            return res;
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        public void PutStake(IEnumerable<StakeItem> stakeEntries)
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "Stake"))
            {
                this.PutStakeInternal(transaction, stakeEntries);
                transaction.Commit();
            }
        }

        /// <summary>
        /// Persists unsaved POS blocks information to the database.
        /// </summary>
        /// <param name="transaction">Open transaction.</param>
        /// <param name="stakeEntries">List of POS block information to be examined and persists if unsaved.</param>
        private void PutStakeInternal(IKeyValueStoreTransaction transaction, IEnumerable<StakeItem> stakeEntries)
        {
            foreach (StakeItem stakeEntry in stakeEntries)
            {
                if (!stakeEntry.InStore)
                {
                    transaction.Insert("Stake", stakeEntry.BlockId.ToBytes(false), this.repositorySerializer.Serialize(stakeEntry.BlockStake));
                    stakeEntry.InStore = true;
                }
            }
        }

        /// <summary>
        /// Retrieves POS blocks information from the database.
        /// </summary>
        /// <param name="blocklist">List of partially initialized POS block information that is to be fully initialized with the values from the database.</param>
        public void GetStake(IEnumerable<StakeItem> blocklist)
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(Stratis.Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, "Stake"))
            {
                foreach (StakeItem blockStake in blocklist)
                {
                    this.logger.LogDebug("Loading POS block hash '{0}' from the database.", blockStake.BlockId);
                    if (transaction.Select("Stake", blockStake.BlockId.ToBytes(false), out BlockStake blockStakeValue))
                    {
                        blockStake.BlockStake = blockStakeValue;
                        blockStake.InStore = true;
                    }
                }
            }
        }

        private void AddBenchStats(StringBuilder log)
        {
            log.AppendLine("======DBCoinView Bench======");

            BackendPerformanceSnapshot snapShot = this.performanceCounter.Snapshot();

            if (this.latestPerformanceSnapShot == null)
                log.AppendLine(snapShot.ToString());
            else
                log.AppendLine((snapShot - this.latestPerformanceSnapShot).ToString());

            this.latestPerformanceSnapShot = snapShot;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
