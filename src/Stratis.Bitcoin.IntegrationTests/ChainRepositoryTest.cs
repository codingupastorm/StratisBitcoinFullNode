using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Base;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Utilities;
using Xunit;
using Stratis.Feature.PoA.Tokenless.Networks;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ChainRepositoryTest
    {
        protected readonly ILoggerFactory loggerFactory;
        private readonly TokenlessNetwork network;
        private readonly RepositorySerializer repositorySerializer;

        /// <summary>
        /// Initializes logger factory for tests in this class.
        /// </summary>
        public ChainRepositoryTest()
        {
            this.loggerFactory = new LoggerFactory();
            this.network = new TokenlessNetwork();
            this.repositorySerializer = new RepositorySerializer(this.network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void CanSaveChainIncrementally()
        {
            DataFolder dataFolder = new DataFolder(TestBase.CreateTestDir(this));

            using (var repo = new ChainRepository(new ChainRepositoryStore(new RepositorySerializer(this.network.Consensus.ConsensusFactory), dataFolder, this.loggerFactory, DateTimeProvider.Default), this.loggerFactory))
            {
                var chain = new ChainIndexer(this.network);

                chain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.True(chain.Tip == chain.Genesis);
                chain = new ChainIndexer(this.network);
                ChainedHeader tip = this.AppendBlock(chain);
                repo.SaveAsync(chain).GetAwaiter().GetResult();
                var newChain = new ChainIndexer(this.network);
                newChain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, newChain.Tip);
                tip = this.AppendBlock(chain);
                repo.SaveAsync(chain).GetAwaiter().GetResult();
                newChain = new ChainIndexer(this.network);
                newChain.SetTip(repo.LoadAsync(chain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, newChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ChainIndexer chain in chainsIndexer)
            {
                Block block = this.network.CreateBlock();
                block.AddTransaction(this.network.CreateTransaction());
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
