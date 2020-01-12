using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class TokenlessTestHelper
    {
        public readonly BlockPolicyEstimator BlockPolicyEstimator;
        private readonly IBlockRepository blockRepository;
        public readonly ICallDataSerializer CallDataSerializer;
        public readonly ChainIndexer ChainIndexer;
        private readonly Mock<ICertificatePermissionsChecker> CertificatePermissionsChecker;
        public readonly InMemoryCoinView InMemoryCoinView;
        public readonly IDateTimeProvider DateTimeProvider;
        public readonly ILoggerFactory LoggerFactory;
        public readonly ITxMempool Mempool;
        public readonly IEnumerable<IMempoolRule> MempoolRules;
        public readonly MempoolSettings MempoolSettings;
        public readonly Network Network;
        public readonly NodeSettings NodeSettings;
        public readonly TokenlessMempoolValidator MempoolValidator;
        public readonly ITokenlessSigner TokenlessSigner;

        public TokenlessTestHelper()
        {
            this.Network = new TokenlessNetwork();
            this.NodeSettings = NodeSettings.Default(this.Network);
            this.LoggerFactory = new ExtendedLoggerFactory();
            this.LoggerFactory.AddConsoleWithFilters();

            this.blockRepository = new Mock<IBlockRepository>().Object;
            this.CallDataSerializer = new NoGasCallDataSerializer(new ContractPrimitiveSerializer(this.Network));

            this.CertificatePermissionsChecker = new Mock<ICertificatePermissionsChecker>();
            this.CertificatePermissionsChecker.Setup(c => c.CheckSenderCertificateHasPermission(It.IsAny<uint160>(), It.IsAny<TransactionSendingPermission>())).Returns(true);

            this.ChainIndexer = new ChainIndexer(this.Network);
            this.InMemoryCoinView = new InMemoryCoinView(this.Network.GenesisHash);
            this.DateTimeProvider = Bitcoin.Utilities.DateTimeProvider.Default;
            this.MempoolSettings = new MempoolSettings(this.NodeSettings) { MempoolExpiry = Bitcoin.Features.MemoryPool.MempoolValidator.DefaultMempoolExpiry };
            this.TokenlessSigner = new TokenlessSigner(this.Network, new SenderRetriever());

            this.BlockPolicyEstimator = new BlockPolicyEstimator(this.MempoolSettings, this.LoggerFactory, this.NodeSettings);
            this.Mempool = new TokenlessMempool(this.BlockPolicyEstimator, this.LoggerFactory, this.NodeSettings);
            this.MempoolRules = CreateMempoolRules();
            this.MempoolValidator = new TokenlessMempoolValidator(this.ChainIndexer, this.InMemoryCoinView, this.DateTimeProvider, this.LoggerFactory, this.Mempool, new MempoolSchedulerLock(), this.MempoolRules, this.MempoolSettings);
        }

        public IEnumerable<IMempoolRule> CreateMempoolRules()
        {
            foreach (Type ruleType in this.Network.Consensus.MempoolRules)
            {
                if (ruleType == typeof(IsSmartContractWellFormedMempoolRule))
                    yield return new IsSmartContractWellFormedMempoolRule(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory, this.CallDataSerializer);
                else if (ruleType == typeof(NoDuplicateTransactionExistOnChainMempoolRule))
                    yield return new NoDuplicateTransactionExistOnChainMempoolRule(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory, this.blockRepository);
                else if (ruleType == typeof(SenderInputMempoolRule))
                    yield return new SenderInputMempoolRule(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory, this.TokenlessSigner, this.CertificatePermissionsChecker.Object);
                else if (ruleType == typeof(CreateTokenlessMempoolEntryRule))
                    yield return new CreateTokenlessMempoolEntryRule(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory);
                else
                    throw new NotImplementedException($"No constructor is defined for '{ruleType.Name}'.");
            }
        }
    }
}
