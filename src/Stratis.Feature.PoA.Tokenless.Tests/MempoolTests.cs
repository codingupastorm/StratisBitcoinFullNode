using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Tokenless
{
    public sealed class MempoolTests
    {
        private readonly BlockPolicyEstimator blockPolicyEstimator;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ChainIndexer chainIndexer;
        private readonly ICoinView coinView;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly ITxMempool mempool;
        private readonly IEnumerable<IMempoolRule> mempoolRules;
        private readonly MempoolSettings mempoolSettings;
        private readonly Network network;
        private readonly NodeSettings nodeSettings;
        private readonly TokenlessMempoolValidator mempoolValidator;

        public MempoolTests()
        {
            this.network = new TokenlessNetwork();

            this.callDataSerializer = new CallDataSerializer(new ContractPrimitiveSerializer(this.network));
            this.chainIndexer = new ChainIndexer(this.network);
            this.coinView = new InMemoryCoinView(this.network.GenesisHash);
            this.dateTimeProvider = DateTimeProvider.Default;
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.nodeSettings = NodeSettings.Default(this.network);
            this.mempoolSettings = new MempoolSettings(this.nodeSettings) { MempoolExpiry = MempoolValidator.DefaultMempoolExpiry };

            this.blockPolicyEstimator = new BlockPolicyEstimator(this.mempoolSettings, this.loggerFactory, this.nodeSettings);
            this.mempool = new TokenlessMempool(this.blockPolicyEstimator, this.loggerFactory, this.nodeSettings);
            this.mempoolRules = CreateMempoolRules();
            this.mempoolValidator = new TokenlessMempoolValidator(this.chainIndexer, this.coinView, this.dateTimeProvider, this.loggerFactory, this.mempool, new MempoolSchedulerLock(), this.mempoolRules, this.mempoolSettings);
        }

        [Fact]
        public async Task SubmitToTokenlessMempool_Accepted_Async()
        {
            var transaction = this.network.CreateTransaction();
            var mempoolValidationState = new MempoolValidationState(false);
            await this.mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transaction);

            Assert.Equal(1, this.mempool.Size);
        }

        [Fact]
        public void SubmitToTokenlessMempool_Failed_Async()
        {

        }

        private IEnumerable<IMempoolRule> CreateMempoolRules()
        {
            foreach (var ruleType in this.network.Consensus.MempoolRules)
            {
                if (ruleType == typeof(IsSmartContractWellFormedMempoolRule))
                    yield return (IMempoolRule)Activator.CreateInstance(ruleType, this.network, this.mempool, this.mempoolSettings, this.chainIndexer, this.loggerFactory, this.callDataSerializer);
                else
                    yield return (IMempoolRule)Activator.CreateInstance(ruleType, this.network, this.mempool, this.mempoolSettings, this.chainIndexer, this.loggerFactory);
            }
        }
    }
}
