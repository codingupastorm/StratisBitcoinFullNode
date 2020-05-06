using System;
using System.Collections.Generic;
using MembershipServices;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Core.AsyncWork;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Fee;
using Stratis.Features.MemoryPool.Interfaces;
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
        public readonly IDateTimeProvider DateTimeProvider;
        public readonly ILoggerFactory LoggerFactory;
        public readonly ITxMempool Mempool;
        public readonly IEnumerable<IMempoolRule> MempoolRules;
        public readonly MempoolSettings MempoolSettings;
        public readonly Network Network;
        public readonly NodeSettings NodeSettings;
        public readonly TokenlessMempoolValidator MempoolValidator;
        public readonly ITokenlessSigner TokenlessSigner;
        public readonly Mock<IMembershipServicesDirectory> MembershipServices;

        public TokenlessTestHelper()
        {
            this.Network = new TokenlessNetwork();
            this.NodeSettings = NodeSettings.Default(this.Network);
            this.LoggerFactory = new ExtendedLoggerFactory();

            this.blockRepository = new Mock<IBlockRepository>().Object;
            this.CallDataSerializer = new NoGasCallDataSerializer(new ContractPrimitiveSerializer(this.Network));

            this.CertificatePermissionsChecker = new Mock<ICertificatePermissionsChecker>();
            this.CertificatePermissionsChecker.Setup(c => c.CheckSenderCertificateHasPermission(It.IsAny<uint160>(), It.IsAny<TransactionSendingPermission>())).Returns(true);

            this.ChainIndexer = new ChainIndexer(this.Network);
            this.DateTimeProvider = Stratis.Core.AsyncWork.DateTimeProvider.Default;
            this.MempoolSettings = new MempoolSettings(this.NodeSettings) { MempoolExpiry = Features.MemoryPool.MempoolValidator.DefaultMempoolExpiry };
            this.TokenlessSigner = new TokenlessSigner(this.Network, new SenderRetriever());

            this.BlockPolicyEstimator = new BlockPolicyEstimator(this.MempoolSettings, this.LoggerFactory, this.NodeSettings);
            this.Mempool = new TokenlessMempool(this.BlockPolicyEstimator, this.LoggerFactory);

            // TODO: Ostensibly need to be able to test the revoked case too
            this.MembershipServices = new Mock<IMembershipServicesDirectory>();
            this.MembershipServices.Setup(c => c.IsCertificateRevoked(It.IsAny<string>())).Returns(false);

            this.MempoolRules = CreateMempoolRules();
            this.MempoolValidator = new TokenlessMempoolValidator(this.ChainIndexer, this.DateTimeProvider, this.LoggerFactory, this.Mempool, new MempoolSchedulerLock(), this.MempoolRules, this.MempoolSettings);
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
                else if (ruleType == typeof(CheckSenderCertificateIsNotRevoked))
                    yield return new CheckSenderCertificateIsNotRevoked(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory, this.MembershipServices.Object, this.TokenlessSigner);
                else
                    throw new NotImplementedException($"No constructor is defined for '{ruleType.Name}'.");
            }
        }
    }
}
