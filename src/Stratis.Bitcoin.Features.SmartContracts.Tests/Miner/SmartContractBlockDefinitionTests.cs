using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Xunit;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Miner
{
    public class SmartContractBlockDefinitionTests
    {
        private readonly Mock<ICoinView> coinView;
        private readonly Mock<IConsensusLoop> consensusLoop;
        private readonly Mock<IConsensusRules> consensusRules;
        private readonly Mock<ITxMempool> txMempool;
        private readonly Mock<IDateTimeProvider> dateTimeProvider;
        private readonly Mock<ISmartContractExecutorFactory> executorFactory;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private RuleContext callbackRuleContext;
        private readonly Money powReward;
        private readonly Mock<MinerSettings> minerSettings;
        private readonly Network network;
        private readonly Mock<ContractStateRepositoryRoot> stateRoot;
        private readonly Key key;

        public SmartContractBlockDefinitionTests()
        {
            this.coinView = new Mock<ICoinView>();
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.consensusRules = new Mock<IConsensusRules>();
            this.txMempool = new Mock<ITxMempool>();
            this.dateTimeProvider = new Mock<IDateTimeProvider>();
            this.executorFactory = new Mock<ISmartContractExecutorFactory>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);
            this.powReward = Money.Coins(50);
            this.network = new SmartContractsRegTest();
            this.network.Consensus.Rules = new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().GetRules();
            this.minerSettings = new Mock<MinerSettings>();
            this.key = new Key();
            this.stateRoot = new Mock<ContractStateRepositoryRoot>();
        }

        // NOTE: Everything here is adapted from PowBlockAssemblerTest for speed.

        [Fact]
        public void AddTransactions_Until_BlockSize()
        {
            ConcurrentChain chain = GenerateChainWithHeight(5, this.network, this.key);
            this.consensusLoop.Setup(c => c.Tip).Returns(chain.GetBlock(5));

            const int numTxs = 10_000;

            Transaction[] txs = new Transaction[numTxs];
            for(int i=0; i< numTxs; i++)
            {
                txs[i] = CreateTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Key(), new uint256(124124));
            }

            var txFee = new Money(1000);
            SetupTxMempool(chain, txFee, txs);

            var blockDefinition = new ScTestBlockDefinition(
                this.coinView.Object,
                this.consensusLoop.Object,
                this.dateTimeProvider.Object,
                this.executorFactory.Object,
                this.loggerFactory.Object,
                this.txMempool.Object,
                new MempoolSchedulerLock(),
                this.minerSettings.Object,
                this.network,
                this.stateRoot.Object
            );

            (Block Block, int Selected, int Updated) result = blockDefinition.AddTransactions();

            Assert.NotEmpty(result.Block.Transactions);

            //Assert.Equal(transaction.ToHex(), result.Block.Transactions[0].ToHex());
            Assert.Equal(1, result.Selected);
            Assert.Equal(0, result.Updated);
        }

        private static ConcurrentChain GenerateChainWithHeightAndActivatedBip9(int blockAmount, Network network, Key key, BIP9DeploymentsParameters parameter, Target bits = null)
        {
            var chain = new ConcurrentChain(network);
            uint nonce = RandomUtils.GetUInt32();
            uint256 prevBlockHash = chain.Genesis.HashBlock;
            for (int i = 0; i < blockAmount; i++)
            {
                Block block = network.Consensus.ConsensusFactory.CreateBlock();
                Transaction coinbase = CreateCoinbaseTransaction(network, key, chain.Height + 1);

                block.AddTransaction(coinbase);
                block.UpdateMerkleRoot();
                block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
                block.Header.HashPrevBlock = prevBlockHash;
                block.Header.Nonce = nonce;

                if (bits != null)
                {
                    block.Header.Bits = bits;
                }

                if (parameter != null)
                {
                    uint version = ThresholdConditionCache.VersionbitsTopBits;
                    version |= ((uint)1) << parameter.Bit;
                    block.Header.Version = (int)version;
                }

                chain.SetTip(block.Header);
                prevBlockHash = block.GetHash();
            }

            return chain;
        }

        private static Transaction CreateCoinbaseTransaction(Network network, Key key, int height)
        {
            var coinbase = new Transaction();
            coinbase.AddInput(TxIn.CreateCoinbase(height));
            coinbase.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinbase;
        }

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network, Key key, Target bits = null)
        {
            return GenerateChainWithHeightAndActivatedBip9(blockAmount, network, key, null, bits);
        }

        private static Transaction CreateTransaction(Network network, Key inkey, int height, Money amount, Key outKey, uint256 prevOutHash)
        {
            var coinbase = new Transaction();
            coinbase.AddInput(new TxIn(new OutPoint(prevOutHash, 1), inkey.ScriptPubKey));
            coinbase.AddOutput(new TxOut(amount, outKey));
            return coinbase;
        }

        private TxMempoolEntry[] SetupTxMempool(ConcurrentChain chain, Money txFee, params Transaction[] transactions)
        {
            uint txTime = Utils.DateTimeToUnixTime(chain.Tip.Header.BlockTime.AddSeconds(25));
            var lockPoints = new LockPoints()
            {
                Height = 4,
                MaxInputBlock = chain.GetBlock(4),
                Time = chain.GetBlock(4).Header.Time
            };

            var resultingTransactionEntries = new List<TxMempoolEntry>();
            var indexedTransactionSet = new TxMempool.IndexedTransactionSet();
            foreach (Transaction transaction in transactions)
            {
                var txPoolEntry = new TxMempoolEntry(transaction, txFee, txTime, 1, 4, new Money(400000000), false, 2, lockPoints, this.network.Consensus.Options);
                indexedTransactionSet.Add(txPoolEntry);
                resultingTransactionEntries.Add(txPoolEntry);
            }


            this.txMempool.Setup(t => t.MapTx)
                .Returns(indexedTransactionSet);

            return resultingTransactionEntries.ToArray();
        }

        /// <summary>
        /// Why are we making classes like this :( 
        /// </summary>
        private class ScTestBlockDefinition : SmartContractBlockDefinition
        {
            public ScTestBlockDefinition(
                ICoinView coinView,
                IConsensusLoop consensusLoop,
                IDateTimeProvider dateTimeProvider,
                ISmartContractExecutorFactory executorFactory,
                ILoggerFactory loggerFactory,
                ITxMempool mempool,
                MempoolSchedulerLock mempoolLock,
                MinerSettings minerSettings,
                Network network,
                ContractStateRepositoryRoot stateRoot)
                : base(coinView, consensusLoop, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, minerSettings, network, stateRoot)
            {
                this.block = this.BlockTemplate.Block;
            }

            public (Block Block, int Selected, int Updated) AddTransactions()
            {
                int selected;
                int updated;
                base.AddTransactions(out selected, out updated);

                return (this.block, selected, updated);
            }
        }
    }
}
