﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Fee;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Features.PoA.ProtocolEncryption;
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
        public readonly Mock<IRevocationChecker> RevocationChecker;
        public readonly Mock<ICertificatesManager> CertificatesManager;

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
            this.DateTimeProvider = Bitcoin.Utilities.DateTimeProvider.Default;
            this.MempoolSettings = new MempoolSettings(this.NodeSettings) { MempoolExpiry = Features.MemoryPool.MempoolValidator.DefaultMempoolExpiry };
            this.TokenlessSigner = new TokenlessSigner(this.Network, new SenderRetriever());

            this.BlockPolicyEstimator = new BlockPolicyEstimator(this.MempoolSettings, this.LoggerFactory, this.NodeSettings);
            this.Mempool = new TokenlessMempool(this.BlockPolicyEstimator, this.LoggerFactory);

            // TODO: Ostensibly need to be able to test the revoked case too
            this.RevocationChecker = new Mock<IRevocationChecker>();
            this.RevocationChecker.Setup(c => c.IsCertificateRevoked(It.IsAny<string>())).Returns(false);

            // TODO: Ostensibly need to be able to test the revoked case too
            this.CertificatesManager = new Mock<ICertificatesManager>();
            this.CertificatesManager.Setup(c => c.IsCertificateRevokedByAddress(It.IsAny<uint160>())).Returns(false);

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
                    yield return new CheckSenderCertificateIsNotRevoked(this.Network, this.Mempool, this.MempoolSettings, this.ChainIndexer, this.LoggerFactory, this.CertificatesManager.Object, this.TokenlessSigner);
                else
                    throw new NotImplementedException($"No constructor is defined for '{ruleType.Name}'.");
            }
        }
    }
}
