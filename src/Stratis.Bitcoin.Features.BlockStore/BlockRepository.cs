using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;
using Stratis.Features.NodeStorage.KeyValueStoreLDB;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// <see cref="IBlockRepository"/> is the interface to all the logics interacting with the blocks stored in the database.
    /// </summary>
    public interface IBlockRepository : IBlockStore
    {
        /// <summary> The dbreeze database engine.</summary>
        IKeyValueStore KeyValueStore { get; }

        /// <summary>
        /// Deletes blocks and indexes for transactions that belong to deleted blocks.
        /// <para>
        /// It should be noted that this does not delete the entries from disk (only the references are removed) and
        /// as such the file size remains the same.
        /// </para>
        /// </summary>
        /// <remarks>TODO: This will need to be revisited once DBreeze has been fixed or replaced with a solution that works.</remarks>
        /// <param name="hashes">List of block hashes to be deleted.</param>
        void DeleteBlocks(List<uint256> hashes);

        /// <summary>
        /// Persist the next block hash and insert new blocks into the database.
        /// </summary>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        /// <param name="blocks">Blocks to be inserted.</param>
        void PutBlocks(HashHeightPair newTip, List<Block> blocks);

        /// <summary>
        /// Get the blocks from the database by using block hashes.
        /// </summary>
        /// <param name="hashes">A list of unique block hashes.</param>
        /// <returns>The blocks (or null if not found) in the same order as the hashes on input.</returns>
        List<Block> GetBlocks(List<uint256> hashes);

        /// <summary>
        /// Wipe out blocks and their transactions then replace with a new block.
        /// </summary>
        /// <param name="newTip">Hash and height of the new repository's tip.</param>
        /// <param name="hashes">List of all block hashes to be deleted.</param>
        /// <exception cref="DBreezeException">Thrown if an error occurs during database operations.</exception>
        void Delete(HashHeightPair newTip, List<uint256> hashes);

        /// <summary>
        /// Determine if a block already exists
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns><c>true</c> if the block hash can be found in the database, otherwise return <c>false</c>.</returns>
        bool Exist(uint256 hash);

        /// <summary>
        /// Iterate over every block in the database.
        /// If <see cref="TxIndex"/> is true, we store the block hash alongside the transaction hash in the transaction table, otherwise clear the transaction table.
        /// </summary>
        void ReIndex();

        /// <summary>
        /// Set whether to index transactions by block hash, as well as storing them inside of the block.
        /// </summary>
        /// <param name="txIndex">Whether to index transactions.</param>
        void SetTxIndex(bool txIndex);

        /// <summary>Hash and height of the repository's tip.</summary>
        HashHeightPair TipHashAndHeight { get; }

        /// <summary> Indicates that the node should store all transaction data in the database.</summary>
        bool TxIndex { get; }
    }

    public class BlockRepository : IBlockRepository
    {
        internal const string BlockTableName = "Block";

        internal const string CommonTableName = "Common";

        internal const string TransactionTableName = "Transaction";

        public IKeyValueStore KeyValueStore { get; }

        private readonly ILogger logger;

        private readonly Network network;

        private static readonly byte[] RepositoryTipKey = new byte[0];

        private static readonly byte[] TxIndexKey = new byte[1];

        /// <inheritdoc />
        public HashHeightPair TipHashAndHeight { get; private set; }

        /// <inheritdoc />
        public bool TxIndex { get; private set; }

        private readonly IReadOnlyDictionary<uint256, Transaction> genesisTransactions;

        public BlockRepository(Network network, ILoggerFactory loggerFactory, INodeStorageProvider nodeStorageProvider)
        {
            Guard.NotNull(network, nameof(network));

            RegisterStoreProvider(nodeStorageProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory));

            this.KeyValueStore = nodeStorageProvider.GetStore("blocks");

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.genesisTransactions = network.GetGenesis().Transactions.ToDictionary(k => k.GetHash());
        }

        public static void RegisterStoreProvider(INodeStorageProvider nodeStorageProvider, DBreezeSerializer dBreezeSerializer)
        {
            nodeStorageProvider.RegisterStoreProvider("blocks", 
                (nsp, repositorySerializer) => new KeyValueStore<KeyValueStoreLDBRepository>(nsp.DataFolder.BlockPath, nsp.LoggerFactory, nsp.DateTimeProvider, repositorySerializer), 
                dBreezeSerializer);
        }

        /// <inheritdoc />
        public virtual void Initialize()
        {
            Block genesis = this.network.GetGenesis();

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                bool doCommit = false;

                if (this.LoadTipHashAndHeight(transaction) == null)
                {
                    this.SaveTipHashAndHeight(transaction, new HashHeightPair(genesis.GetHash(), 0));
                    doCommit = true;
                }

                if (this.LoadTxIndex(transaction) == null)
                {
                    this.SaveTxIndex(transaction, false);
                    doCommit = true;
                }

                if (doCommit) transaction.Commit();
            }
        }

        /// <inheritdoc />
        public Transaction GetTransactionById(uint256 trxid)
        {
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[TX_INDEXING_DISABLED]:null");
                return default(Transaction);
            }

            if (this.genesisTransactions.TryGetValue(trxid, out Transaction genesisTransaction))
            {
                return genesisTransaction;
            }

            Transaction res = null;
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (!transaction.Select(TransactionTableName, trxid, out uint256 blockHash))
                {
                    this.logger.LogTrace("(-)[NO_BLOCK]:null");
                    return null;
                }

                if (transaction.Select(BlockTableName, blockHash, out Block block))
                {
                    res = block.Transactions.FirstOrDefault(t => t.GetHash() == trxid);
                }
            }

            return res;
        }

        /// <inheritdoc/>
        public Transaction[] GetTransactionsByIds(uint256[] trxids, CancellationToken cancellation = default(CancellationToken))
        {
            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[TX_INDEXING_DISABLED]:null");
                return null;
            }

            Transaction[] txes = new Transaction[trxids.Length];

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                for (int i = 0; i < trxids.Length; i++)
                {
                    cancellation.ThrowIfCancellationRequested();

                    bool alreadyFetched = trxids.Take(i).Any(x => x == trxids[i]);

                    if (alreadyFetched)
                    {
                        this.logger.LogDebug("Duplicated transaction encountered. Tx id: '{0}'.", trxids[i]);

                        txes[i] = txes.First(x => x.GetHash() == trxids[i]);
                        continue;
                    }

                    if (this.genesisTransactions.TryGetValue(trxids[i], out Transaction genesisTransaction))
                    {
                        txes[i] = genesisTransaction;
                        continue;
                    }

                    if (!transaction.Select(TransactionTableName, trxids[i], out uint256 blockHash))
                    {
                        this.logger.LogTrace("(-)[NO_TX_ROW]:null");
                        return null;
                    }

                    if (!transaction.Select(BlockTableName, blockHash, out Block block))
                    {
                        this.logger.LogTrace("(-)[NO_BLOCK]:null");
                        return null;
                    }

                    Transaction tx = block.Transactions.FirstOrDefault(t => t.GetHash() == trxids[i]);

                    txes[i] = tx;
                }
            }

            return txes;
        }

        /// <inheritdoc />
        public uint256 GetBlockIdByTransactionId(uint256 trxid)
        {
            Guard.NotNull(trxid, nameof(trxid));

            if (!this.TxIndex)
            {
                this.logger.LogTrace("(-)[NO_TXINDEX]:null");
                return default(uint256);
            }

            if (this.genesisTransactions.ContainsKey(trxid))
            {
                return this.network.GenesisHash;
            }

            uint256 res = null;
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (transaction.Select(TransactionTableName, trxid, out uint256 blockHash))
                    res = blockHash;
            }

            return res;
        }

        protected virtual void OnInsertBlocks(IKeyValueStoreTransaction dbTransaction, List<Block> blocks)
        {
            var transactions = new List<(Transaction, Block)>();

            bool[] blockExists = dbTransaction.ExistsMultiple<uint256>(BlockTableName, blocks.Select(b => b.GetHash()).ToArray());

            blocks = blocks.Where((b, n) => !blockExists[n]).ToList();

            dbTransaction.InsertMultiple(BlockTableName, blocks.Select(b => (b.GetHash(), b)).ToArray());

            // Index blocks.
            if (this.TxIndex)
            {
                foreach (Block block in blocks)
                {
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));
                }

                this.OnInsertTransactions(dbTransaction, transactions);
            }
        }

        protected virtual void OnInsertTransactions(IKeyValueStoreTransaction dbTransaction, List<(Transaction, Block)> transactions)
        {
            dbTransaction.InsertMultiple(TransactionTableName, transactions.Select(tb => (tb.Item1.GetHash(), tb.Item2.GetHash())).ToArray());
        }

        /// <inheritdoc />
        public void ReIndex()
        {
            using (IKeyValueStoreTransaction dbTransaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, BlockTableName, TransactionTableName))
            {
                if (this.TxIndex)
                {
                    int rowCount = 0;
                    // Insert transactions to database.

                    var totalBlocksCount = dbTransaction.Count(BlockTableName);

                    var warningMessage = new StringBuilder();
                    warningMessage.AppendLine("".PadRight(59, '=') + " W A R N I N G " + "".PadRight(59, '='));
                    warningMessage.AppendLine();
                    warningMessage.AppendLine($"Starting ReIndex process on a total of {totalBlocksCount} blocks.");
                    warningMessage.AppendLine("The operation could take a long time, please don't stop it.");
                    warningMessage.AppendLine();
                    warningMessage.AppendLine("".PadRight(133, '='));
                    warningMessage.AppendLine();

                    this.logger.LogInformation(warningMessage.ToString());

                    foreach ((uint256 blockHash, Block block) in dbTransaction.SelectForward<uint256, Block>(BlockTableName))
                    {
                        dbTransaction.InsertMultiple(TransactionTableName, block.Transactions.Select(t => (t.GetHash(), blockHash)).ToArray());

                        // inform the user about the ongoing operation
                        if (++rowCount % 1000 == 0)
                        {
                            this.logger.LogInformation("Reindex in process... {0}/{1} blocks processed.", rowCount, totalBlocksCount);
                        }
                    }

                    this.logger.LogInformation("Reindex completed successfully.");
                }
                else
                {
                    // Clear tx from database.
                    dbTransaction.RemoveAllKeys(TransactionTableName);
                }

                dbTransaction.Commit();
            }
        }

        /// <inheritdoc />
        public void PutBlocks(HashHeightPair newTip, List<Block> blocks)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(blocks, nameof(blocks));

            // DBreeze is faster if sort ascending by key in memory before insert
            // however we need to find how byte arrays are sorted in DBreeze.
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, BlockTableName, TransactionTableName, CommonTableName))
            {
                this.OnInsertBlocks(transaction, blocks);

                // Commit additions
                this.SaveTipHashAndHeight(transaction, newTip);
                transaction.Commit();
            }
        }

        private bool? LoadTxIndex(IKeyValueStoreTransaction dbTransaction)
        {
            bool? res = null;
            if (dbTransaction.Select(CommonTableName, TxIndexKey, out bool txIndex))
            {
                this.TxIndex = txIndex;
                res = txIndex;
            }

            return res;
        }

        private void SaveTxIndex(IKeyValueStoreTransaction dbTransaction, bool txIndex)
        {
            this.TxIndex = txIndex;
            dbTransaction.Insert(CommonTableName, TxIndexKey, txIndex);
        }

        /// <inheritdoc />
        public void SetTxIndex(bool txIndex)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                this.SaveTxIndex(transaction, txIndex);
                transaction.Commit();
            }
        }

        private HashHeightPair LoadTipHashAndHeight(IKeyValueStoreTransaction dbTransaction)
        {
            if (this.TipHashAndHeight == null && dbTransaction.Select(CommonTableName, RepositoryTipKey, out HashHeightPair hashHeightPair))
                this.TipHashAndHeight = hashHeightPair;

            return this.TipHashAndHeight;
        }

        private void SaveTipHashAndHeight(IKeyValueStoreTransaction dbTransaction, HashHeightPair newTip)
        {
            this.TipHashAndHeight = newTip;
            dbTransaction.Insert(CommonTableName, RepositoryTipKey, newTip);
        }

        /// <inheritdoc />
        public Block GetBlock(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            if (hash == this.network.GenesisHash)
                return this.network.GetGenesis();

            Block res = null;
            using (IKeyValueStoreTransaction dbTransaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (dbTransaction.Select(BlockTableName, hash, out Block block))
                    res = block;
            }

            return res;
        }

        /// <inheritdoc />
        public List<Block> GetBlocks(List<uint256> hashes)
        {
            Guard.NotNull(hashes, nameof(hashes));

            List<Block> blocks;

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                blocks = this.GetBlocksFromHashes(transaction, hashes);
            }

            return blocks;
        }

        /// <inheritdoc />
        public bool Exist(uint256 hash)
        {
            Guard.NotNull(hash, nameof(hash));

            bool res = false;
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (transaction.Exists(BlockTableName, hash))
                    res = true;
            }

            return res;
        }

        protected virtual void OnDeleteTransactions(IKeyValueStoreTransaction dbTransaction, List<(Transaction, Block)> transactions)
        {
            foreach ((Transaction transaction, Block block) in transactions)
                dbTransaction.RemoveKey(TransactionTableName, transaction.GetHash(), transaction);
        }

        protected virtual void OnDeleteBlocks(IKeyValueStoreTransaction dbTransaction, List<Block> blocks)
        {
            if (this.TxIndex)
            {
                var transactions = new List<(Transaction, Block)>();

                foreach (Block block in blocks)
                    foreach (Transaction transaction in block.Transactions)
                        transactions.Add((transaction, block));

                this.OnDeleteTransactions(dbTransaction, transactions);
            }

            foreach (Block block in blocks)
                dbTransaction.RemoveKey(BlockTableName, block.GetHash(), block);
        }

        public List<Block> GetBlocksFromHashes(IKeyValueStoreTransaction dbTransaction, List<uint256> hashes)
        {
            List<Block> blocks = dbTransaction.SelectMultiple<uint256, Block>(BlockTableName, hashes.ToArray());
            for (int i = 0; i < blocks.Count; i++)
                if (hashes[i] == this.network.GenesisHash)
                    blocks[i] = this.network.GetGenesis();

            return blocks;
        }

        /// <inheritdoc />
        public void Delete(HashHeightPair newTip, List<uint256> hashes)
        {
            Guard.NotNull(newTip, nameof(newTip));
            Guard.NotNull(hashes, nameof(hashes));

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, BlockTableName, CommonTableName, TransactionTableName))
            {
                List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);
                this.OnDeleteBlocks(transaction, blocks.Where(b => b != null).ToList());
                this.SaveTipHashAndHeight(transaction, newTip);
                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public void DeleteBlocks(List<uint256> hashes)
        {
            Guard.NotNull(hashes, nameof(hashes));

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, BlockTableName, CommonTableName, TransactionTableName))
            {
                List<Block> blocks = this.GetBlocksFromHashes(transaction, hashes);

                this.OnDeleteBlocks(transaction, blocks.Where(b => b != null).ToList());

                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
