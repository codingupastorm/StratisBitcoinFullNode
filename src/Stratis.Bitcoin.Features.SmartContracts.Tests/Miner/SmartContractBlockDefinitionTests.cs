using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.Executor.Reflection;
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
        private readonly Mock<ISenderRetriever> senderRetriever;
        private readonly Mock<IContractStateRoot> stateRoot;
        private readonly Key key;

        public SmartContractBlockDefinitionTests()
        {
            this.coinView = new Mock<ICoinView>();

            this.consensusRules = new Mock<IConsensusRules>();

            var headerVersionRule = new Mock<HeaderVersionRule>();
            headerVersionRule.Setup(x => x.ComputeBlockVersion(It.IsAny<ChainedHeader>()));
            this.consensusRules.Setup(x => x.GetRule<HeaderVersionRule>()).Returns(headerVersionRule.Object);
            var coinViewRule = new Mock<CoinViewRule>();
            coinViewRule.Setup(x => x.GetProofOfWorkReward(It.IsAny<int>())).Returns(50 * Money.COIN);
            coinViewRule.Setup(x => x.GetBlockWeight(It.IsAny<Block>())).Returns(0);
            this.consensusRules.Setup(x => x.GetRule<CoinViewRule>()).Returns(coinViewRule.Object);
            this.consensusLoop = new Mock<IConsensusLoop>();
            this.consensusLoop.Setup(x=> x.ConsensusRules).Returns(this.consensusRules.Object);

            this.txMempool = new Mock<ITxMempool>();

            this.dateTimeProvider = new Mock<IDateTimeProvider>();

            var executor = new Mock<ISmartContractExecutor>();
            executor.Setup(x => x.Execute(It.IsAny<ISmartContractTransactionContext>())).Returns(new SmartContractExecutionResult
            {
                
            });
            this.executorFactory = new Mock<ISmartContractExecutorFactory>();
            this.executorFactory.Setup(x => x.CreateExecutor(It.IsAny<IContractState>(), It.IsAny<ISmartContractTransactionContext>())).Returns(executor.Object);

            this.loggerFactory = new Mock<ILoggerFactory>();
            this.loggerFactory.Setup(l => l.CreateLogger(It.IsAny<string>()))
               .Returns(new Mock<ILogger>().Object);

            this.powReward = Money.Coins(50);

            this.network = new SmartContractsRegTest();
            this.network.Consensus.Rules = new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().GetRules();

            this.minerSettings = new Mock<MinerSettings>();

            this.key = new Key();

            this.senderRetriever = new Mock<ISenderRetriever>();
            this.senderRetriever.Setup(x => x.GetSender(It.IsAny<Transaction>(), It.IsAny<ICoinView>(), It.IsAny<IList<Transaction>>())).Returns(GetSenderResult.CreateSuccess(new uint160(0)));
            this.senderRetriever.Setup(x => x.GetAddressFromScript(It.IsAny<Script>())).Returns(GetSenderResult.CreateSuccess(new uint160(0)));

            var nestedStateRoot = new Mock<IContractStateRoot>();
            nestedStateRoot.SetupGet(x => x.Root).Returns(new byte[32]);
            this.stateRoot = new Mock<IContractStateRoot>();
            this.stateRoot.Setup(x => x.GetSnapshotTo(It.IsAny<byte[]>())).Returns(nestedStateRoot.Object);
        }

        // TODO: 
        // - Use a test value for minerSettings.BlockDefinitionOptions.BlockMaxSize.
        // - Insert 1 SC transaction so there is a refund.
        // - Insert another SC transaction but ensure it doesn't get added when total size including refund goes over max.

        [Fact]
        public void AddTransactions_Until_BlockSize()
        {
            ConcurrentChain chain = GenerateChainWithHeight(5, this.network, this.key);
            this.consensusLoop.Setup(c => c.Tip).Returns(chain.GetBlock(5));
            this.minerSettings.SetupGet(x => x.BlockDefinitionOptions).Returns(new BlockDefinitionOptions(1_500, 1_500));

            const int numTxs = 2;

            Transaction[] txs = new Transaction[numTxs];
            for(int i=0; i < numTxs; i++)
            {
                SmartContractCarrier scCarrier = SmartContractCarrier.CallContract(1, new uint160( (ulong) i), "Test", 1, (Gas) 10_000);
                txs[i] = CreateScTransaction(this.network, this.key, 5, new Money(400 * 1000 * 1000), new Script(scCarrier.Serialize()), new uint256(124124));
            }

            var txFee = new Money(1000);
            SetupTxMempool(chain, txFee, txs);

            var scBlockDefinition = new SmartContractBlockDefinition(
                this.coinView.Object,
                this.consensusLoop.Object,
                this.dateTimeProvider.Object,
                this.executorFactory.Object,
                this.loggerFactory.Object,
                this.txMempool.Object,
                new MempoolSchedulerLock(),
                this.minerSettings.Object,
                this.network,
                this.senderRetriever.Object,
                this.stateRoot.Object);

            var blockTemplate = scBlockDefinition.Build(chain.Tip, new Key().ScriptPubKey);

        }

        /*
         *Below ripped from PowBlockAssemblerTests.cs for speed. 
         */

        private static ConcurrentChain GenerateChainWithHeight(int blockAmount, Network network, Key key)
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

        private static Transaction CreateScTransaction(Network network, Key inkey, int height, Money amount, Script script, uint256 prevOutHash)
        {
            var tx = new Transaction();
            tx.AddInput(new TxIn(new OutPoint(prevOutHash, 1), inkey.ScriptPubKey));
            tx.AddOutput(new TxOut(amount, script));
            return tx;
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
        
    }
}
