using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.BlockStore.Tests
{
    public class BlockRepositoryTests : LogsTestBase
    {
        private BlockKeyValueStore keyValueStore = null;

        private void SetBlockKeyValueStore(string dir)
        {
            this.keyValueStore = new BlockKeyValueStore(new RepositorySerializer(this.Network.Consensus.ConsensusFactory), new DataFolder(dir), this.LoggerFactory.Object, DateTimeProvider.Default);
        }

        [Fact]
        public void InitializesGenesisBlockAndTxIndexOnFirstLoad()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
            }

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                Assert.True(transaction.Select<byte[], HashHeightPair>("Common", new byte[0], out HashHeightPair savedTip));
                transaction.Select<byte[], bool>("Common", new byte[1], out bool txIndex);

                Assert.Equal(this.Network.GetGenesis().GetHash(), savedTip.Hash);
                Assert.False(txIndex);
            }
        }

        [Fact]
        public void DoesNotOverwriteExistingBlockAndTxIndexOnFirstLoad()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            // Initialize the repo.
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<byte[], byte[]>("Common", new byte[0], this.RepositorySerializer.Serialize(new HashHeightPair(new uint256(56), 1)));
                transaction.Insert<byte[], byte[]>("Common", new byte[1], new byte[] { 1 });
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
            }

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.True(transaction.Select<byte[], HashHeightPair>("Common", new byte[0], out HashHeightPair storedTip));
                Assert.True(transaction.Select<byte[], bool>("Common", new byte[1], out bool txIndex));

                Assert.Equal(new HashHeightPair(new uint256(56), 1), storedTip);
                Assert.True(txIndex);
            }
        }

        [Fact]
        public void GetTrxAsyncWithoutTransactionIndexReturnsNewTransaction()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Equal(default(Transaction), repository.GetTransactionById(uint256.Zero));
            }
        }

        [Fact]
        public void GetTrxAsyncWithoutTransactionInIndexReturnsNull()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                var blockId = new uint256(8920);
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Null(repository.GetTransactionById(new uint256(65)));
            }
        }

        [Fact]
        public void GetTrxAsyncWithTransactionReturnsExistingTransaction()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Transaction trans = this.Network.CreateTransaction();
            trans.Version = 125;

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                Block block = this.Network.CreateBlock();
                block.Header.GetHash();
                block.Transactions.Add(trans);

                transaction.Insert("Block", new BlockTableKey(1, block.Header.GetHash()), block);
                transaction.Insert<uint256, uint256>("Transaction", trans.GetHash(), block.Header.GetHash());
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Equal((uint)125, repository.GetTransactionById(trans.GetHash()).Version);
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutTxIndexReturnsDefaultId()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], false);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Equal(default(uint256), repository.GetBlockIdByTransactionId(new uint256(26)));
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithoutExistingTransactionReturnsNull()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Null(repository.GetBlockIdByTransactionId(new uint256(26)));
            }
        }

        [Fact]
        public void GetTrxBlockIdAsyncWithTransactionReturnsBlockId()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<uint256, uint256>("Transaction", new uint256(26), new uint256(42));
                transaction.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                transaction.Insert<byte[], bool>("Common", new byte[1], true);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Equal(new uint256(42), repository.GetBlockIdByTransactionId(new uint256(26)));
            }
        }

        [Fact]
        public void PutAsyncWritesBlocksAndTxsToDbAndSavesNextBlockHash()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            var nextBlockHash = new uint256(1241256);
            var blocks = new List<Block>();
            Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
            BlockHeader blockHeader = block.Header;
            blockHeader.Bits = new Target(12);
            Transaction transaction = this.Network.CreateTransaction();
            transaction.Version = 32;
            block.Transactions.Add(transaction);
            transaction = this.Network.CreateTransaction();
            transaction.Version = 48;
            block.Transactions.Add(transaction);
            blocks.Add(block);

            Block block2 = this.Network.Consensus.ConsensusFactory.CreateBlock();
            block2.Header.Nonce = 11;
            transaction = this.Network.CreateTransaction();
            transaction.Version = 15;
            block2.Transactions.Add(transaction);
            blocks.Add(block2);

            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                trans.Insert<byte[], HashHeightPair>("Common", new byte[0], new HashHeightPair(uint256.Zero, 1));
                trans.Insert<byte[], bool>("Common", new byte[1], true);
                trans.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.PutBlocks(new HashHeightPair(nextBlockHash, 100), blocks);
            }

            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.True(trans.Select<byte[], HashHeightPair>("Common", new byte[0], out HashHeightPair blockHashKey));
                var blockDict = trans.SelectDictionary<BlockTableKey, Block>("Block");
                var transDict = trans.SelectDictionary<uint256, uint256>("Transaction");

                Assert.Equal(new HashHeightPair(nextBlockHash, 100), blockHashKey);
                Assert.Equal(2, blockDict.Count);
                Assert.Equal(3, transDict.Count);

                foreach (KeyValuePair<BlockTableKey, Block> item in blockDict)
                {
                    Block bl = blocks.Single(b => b.GetHash() == item.Key.Hash);
                    Assert.Equal(bl.Header.GetHash(), item.Value.Header.GetHash());
                }

                foreach (KeyValuePair<uint256, uint256> item in transDict)
                {
                    Block bl = blocks.Single(b => b.Transactions.Any(t => t.GetHash() == item.Key));
                    Assert.Equal(bl.GetHash(), item.Value);
                }
            }
        }

        [Fact]
        public void SetTxIndexUpdatesTxIndex()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                trans.Insert<byte[], byte[]>("Common", new byte[1], new byte[] { 1 });
                trans.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(false);
            }

            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.True(trans.Select<byte[], bool>("Common", new byte[1], out bool txIndex));
                Assert.False(txIndex);
            }
        }

        [Fact]
        public void GetAsyncWithExistingBlockReturnsBlock()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
            var blockTableKey = new BlockTableKey(1, block.GetHash());

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert("Block", blockTableKey, block);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Equal(block.GetHash(), repository.GetBlock(block.GetHash()).GetHash());
            }
        }

        [Fact]
        public void GetAsyncWithExistingBlocksReturnsBlocks()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            var blocks = new Block[10];

            blocks[0] = this.Network.Consensus.ConsensusFactory.CreateBlock();
            for (int i = 1; i < blocks.Length; i++)
            {
                blocks[i] = this.Network.Consensus.ConsensusFactory.CreateBlock();
                blocks[i].Header.HashPrevBlock = blocks[i - 1].Header.GetHash();
            }

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    var blockTableKey = new BlockTableKey(i + 1, blocks[i].GetHash());
                    transaction.Insert("Block", blockTableKey, blocks[i]);
                }

                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                List<Block> result = repository.GetBlocks(blocks.Select(b => b.GetHash()).ToList());

                Assert.Equal(blocks.Length, result.Count);
                for (int i = 0; i < 10; i++)
                    Assert.Equal(blocks[i].GetHash(), result[i].GetHash());
            }
        }

        [Fact]
        public void GetAsyncWithoutExistingBlockReturnsNull()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.Null(repository.GetBlock(new uint256()));
            }
        }

        [Fact]
        public void ExistAsyncWithExistingBlockReturnsTrue()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
            BlockTableKey blockTableKey = new BlockTableKey(1, block.GetHash());

            // Initialize the repo.
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert("Block", blockTableKey, block);
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.True(repository.Exist(block.GetHash()));
            }
        }

        [Fact]
        public void ExistAsyncWithoutExistingBlockReturnsFalse()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Assert.False(repository.Exist(new uint256()));
            }
        }

        [Fact]
        public void DeleteAsyncRemovesBlocksAndTransactions()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Block block = this.Network.CreateBlock();
            block.Transactions.Add(this.Network.CreateTransaction());

            BlockTableKey blockTableKey = new BlockTableKey(1, block.GetHash());

            // Initialize the repo.
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<BlockTableKey, Block>("Block", blockTableKey, block);
                transaction.Insert<uint256, uint256>("Transaction", block.Transactions[0].GetHash(), block.GetHash());
                transaction.Insert<byte[], byte[]>("Common", new byte[1], new byte[] { 1 });
                transaction.Commit();
            }

            var tip = new HashHeightPair(new uint256(45), 100);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.Delete(tip, new List<uint256> { block.GetHash() });
            }

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.True(transaction.Select<byte[], HashHeightPair>("Common", new byte[0], out HashHeightPair storedTip));
                Dictionary<uint256, Block> blockDict = transaction.SelectDictionary<uint256, Block>("Block");
                Dictionary<uint256, uint256> transDict = transaction.SelectDictionary<uint256, uint256>("Transaction");

                Assert.Equal(tip, storedTip);
                Assert.Empty(blockDict);
                Assert.Empty(transDict);
            }
        }

        [Fact]
        public void ReIndexAsync_TxIndex_OffToOn()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Block block = this.Network.CreateBlock();
            Transaction transaction = this.Network.CreateTransaction();
            block.Transactions.Add(transaction);

            var blockTableKey = new BlockTableKey(1, block.GetHash());

            // Set up database to mimic that created when TxIndex was off. No transactions stored.
            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                trans.Insert("Block", blockTableKey, block);
                trans.Commit();
            }

            // Turn TxIndex on and then reindex database, as would happen on node startup if -txindex and -reindex are set.
            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(true);
                repository.ReIndex();
            }

            // Check that after indexing database, the transaction inside the block is now indexed.
            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                var blockDict = trans.SelectDictionary<BlockTableKey, Block>("Block");
                var transDict = trans.SelectDictionary<uint256, uint256>("Transaction");

                // Block stored as expected.
                Assert.Single(blockDict);
                Assert.Equal(block.GetHash(), blockDict.FirstOrDefault().Value.GetHash());

                // Transaction row in database stored as expected.
                Assert.Single(transDict);
                KeyValuePair<uint256, uint256> savedTransactionRow = transDict.FirstOrDefault();
                Assert.Equal(transaction.GetHash(), savedTransactionRow.Key);
                Assert.Equal(block.GetHash(), savedTransactionRow.Value);
            }
        }

        [Fact]
        public void ReIndexAsync_TxIndex_OnToOff()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            Block block = this.Network.CreateBlock();
            Transaction transaction = this.Network.CreateTransaction();
            block.Transactions.Add(transaction);

            var blockTableKey = new BlockTableKey(1, block.GetHash());

            // Set up database to mimic that created when TxIndex was on. Transaction from block is stored.
            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                trans.Insert<BlockTableKey, Block>("Block", blockTableKey, block);
                trans.Insert<uint256, uint256>("Transaction", transaction.GetHash(), block.GetHash());
                trans.Commit();
            }

            // Turn TxIndex off and then reindex database, as would happen on node startup if -txindex=0 and -reindex are set.
            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(false);
                repository.ReIndex();
            }

            // Check that after indexing database, the transaction is no longer stored.
            using (IKeyValueStoreTransaction trans = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                var blockDict = trans.SelectDictionary<BlockTableKey, Block>("Block");
                var transDict = trans.SelectDictionary<uint256, uint256>("Transaction");

                // Block still stored as expected.
                Assert.Single(blockDict);
                Assert.Equal(block.GetHash(), blockDict.FirstOrDefault().Value.GetHash());

                // No transactions indexed.
                Assert.Empty(transDict);
            }
        }

        [Fact]
        public void GetBlockByHashReturnsGenesisBlock()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                Block genesis = repository.GetBlock(this.Network.GetGenesis().GetHash());

                Assert.Equal(this.Network.GetGenesis().GetHash(), genesis.GetHash());
            }
        }

        [Fact]
        public void GetBlocksByHashReturnsGenesisBlock()
        {
            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                List<Block> results = repository.GetBlocks(new List<uint256> { this.Network.GetGenesis().GetHash() });

                Assert.NotEmpty(results);
                Assert.NotNull(results.First());
                Assert.Equal(this.Network.GetGenesis().GetHash(), results.First().GetHash());
            }
        }

        [Fact]
        public void GetTransactionByIdForGenesisBlock()
        {
            var genesis = this.Network.GetGenesis();
            var genesisTransactions = genesis.Transactions;

            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(true);

                foreach (var transaction in genesisTransactions)
                {
                    var result = repository.GetTransactionById(transaction.GetHash());

                    Assert.NotNull(result);
                    Assert.Equal(transaction.GetHash(), result.GetHash());
                }
            }
        }

        [Fact]
        public void GetTransactionsByIdsForGenesisBlock()
        {
            var genesis = this.Network.GetGenesis();
            var genesisTransactions = genesis.Transactions;

            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(true);

                var result = repository.GetTransactionsByIds(genesis.Transactions.Select(t => t.GetHash()).ToArray());

                Assert.NotNull(result);

                for (var i = 0; i < genesisTransactions.Count; i++)
                {
                    Assert.Equal(genesisTransactions[i].GetHash(), result[i].GetHash());
                }
            }
        }

        [Fact]
        public void GetBlockIdByTransactionIdForGenesisBlock()
        {
            var genesis = this.Network.GetGenesis();
            var genesisTransactions = genesis.Transactions;

            string dir = CreateTestDir(this);
            SetBlockKeyValueStore(dir);

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(true);

                foreach (var transaction in genesisTransactions)
                {
                    var result = repository.GetBlockIdByTransactionId(transaction.GetHash());

                    Assert.NotNull(result);
                    Assert.Equal(this.Network.GenesisHash, result);
                }
            }
        }

        [Fact]
        public void TransactionsExist()
        {
            SetBlockKeyValueStore(CreateTestDir(this));

            Block block = this.Network.CreateBlock();

            var blockTableKey = new BlockTableKey(1, block.GetHash());

            Transaction tx1 = this.Network.CreateTransaction();
            Transaction tx2 = this.Network.CreateTransaction();
            Transaction tx3 = this.Network.CreateTransaction();

            block.AddTransaction(tx1);
            block.AddTransaction(tx2);
            block.AddTransaction(tx3);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {

                transaction.Insert("Block", blockTableKey, block);
                transaction.Insert<uint256, uint256>("Transaction", tx1.GetHash(), block.Header.GetHash());
                transaction.Insert<uint256, uint256>("Transaction", tx2.GetHash(), block.Header.GetHash());
                transaction.Insert<uint256, uint256>("Transaction", tx3.GetHash(), block.Header.GetHash());
                transaction.Commit();
            }

            using (IBlockRepository repository = this.SetupRepository(this.Network))
            {
                repository.SetTxIndex(true);

                Assert.True(repository.TransactionExists(tx1.GetHash()));
                Assert.True(repository.TransactionExists(tx2.GetHash()));
                Assert.True(repository.TransactionExists(tx3.GetHash()));
                Assert.False(repository.TransactionExists(new uint256(0)));
            }
        }

        private IBlockRepository SetupRepository(Network main)
        {
            var repository = new BlockRepository(main, this.LoggerFactory.Object, this.keyValueStore, this.RepositorySerializer);
            repository.Initialize();

            return repository;
        }
    }
}
