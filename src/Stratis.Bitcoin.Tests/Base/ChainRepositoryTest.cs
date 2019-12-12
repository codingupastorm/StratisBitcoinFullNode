using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        private readonly DBreezeSerializer dBreezeSerializer;

        public ChainRepositoryTest() : base(KnownNetworks.StratisRegTest)
        {
            this.dBreezeSerializer = new DBreezeSerializer(this.Network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ChainIndexer(KnownNetworks.StratisRegTest);
            this.AppendBlock(chain);

            var keyValueStore = new ChainRepositoryStore(this.Network, new DataFolder(dir), new LoggerFactory(), DateTimeProvider.Default);

            using (var repo = new ChainRepository(keyValueStore, new LoggerFactory()))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (IKeyValueStoreTransaction transaction = keyValueStore.CreateTransaction(Interfaces.KeyValueStoreTransactionMode.Read))
            {
                ChainedHeader tip = null;
                foreach ((int key, BlockHeader blockHeader) in transaction.SelectForward<int, BlockHeader>("Chain"))
                {
                    if (tip != null && blockHeader.HashPrevBlock != tip.HashBlock)
                        break;
                    tip = new ChainedHeader(blockHeader, blockHeader.GetHash(), tip);
                }
                Assert.Equal(tip, chain.Tip);
            }
        }

        [Fact]
        public void GetChainReturnsConcurrentChainFromDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ChainIndexer(KnownNetworks.StratisRegTest);
            ChainedHeader tip = this.AppendBlock(chain);

            var keyValueStore = new ChainRepositoryStore(this.Network, new DataFolder(dir), new LoggerFactory(), DateTimeProvider.Default);

            using (IKeyValueStoreTransaction transaction = keyValueStore.CreateTransaction(Interfaces.KeyValueStoreTransactionMode.ReadWrite))
            {
                ChainedHeader toSave = tip;
                var blocks = new List<ChainedHeader>();
                while (toSave != null)
                {
                    blocks.Insert(0, toSave);
                    toSave = toSave.Previous;
                }

                foreach (ChainedHeader block in blocks)
                {
                    transaction.Insert("Chain", block.Height, block.Header);
                }

                transaction.Commit();
            }
            using (var repo = new ChainRepository(keyValueStore, new LoggerFactory()))
            {
                var testChain = new ChainIndexer(KnownNetworks.StratisRegTest);
                testChain.SetTip(repo.LoadAsync(testChain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, testChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ChainIndexer chain in chainsIndexer)
            {
                Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chainsIndexer);
        }
    }
}