﻿using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Core.P2P.Peer;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.Features.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementRequestHandlerTests
    {
        private readonly Network network;
        private readonly ICallDataSerializer callDataSerializer;

        public EndorsementRequestHandlerTests()
        {
            this.network = new TokenlessNetwork();
            this.callDataSerializer = new NoGasCallDataSerializer(new ContractPrimitiveSerializer(this.network));
        }

        [Fact]
        public async Task ExecutionSucceedsAndTransactionIsSignedAsync()
        {
            const int height = 16;
            uint160 sender = uint160.One;
            uint160 contract = new uint160(RandomUtils.GetBytes(20));
            var policy = new EndorsementPolicy();

            var validatorMock = new Mock<IEndorsementRequestValidator>();
            validatorMock.Setup(x => x.ValidateRequest(It.IsAny<EndorsementRequest>()))
                .Returns(true);

            var signerMock = new Mock<IEndorsementSigner>();

            var stateRootMock = new Mock<IStateRepositoryRoot>();
            stateRootMock.Setup(s => s.GetPolicy(It.IsAny<uint160>()))
                .Returns(policy);

            var readWriteSetBuilder = new ReadWriteSetBuilder();

            var executorMock = new Mock<IContractExecutor>();
            executorMock.Setup(x => x.Execute(It.IsAny<IContractTransactionContext>()))
                .Returns(new SmartContractExecutionResult
                {
                    ReadWriteSet = readWriteSetBuilder,
                    PrivateReadWriteSet = readWriteSetBuilder,
                    Revert = false,
                    To = contract
                });

            var executorFactoryMock = new Mock<IContractExecutorFactory>();
            executorFactoryMock.Setup(x => x.CreateExecutor(It.IsAny<IStateRepositoryRoot>()))
                .Returns(executorMock.Object);

            var tokenlessSignerMock = new Mock<ITokenlessSigner>();
            tokenlessSignerMock.Setup(x => x.GetSender(It.IsAny<Transaction>()))
                .Returns(GetSenderResult.CreateSuccess(sender));

            var consensusManagerMock = new Mock<IConsensusManager>();
            consensusManagerMock.Setup(x => x.Tip)
                .Returns(new ChainedHeader(new SmartContractBlockHeader(), uint256.One, height));

            var proposalResponse = new SignedProposalResponse
            {
                Endorsement = new Endorsement.Endorsement(new byte[] { }, new byte[] { }),
                ProposalResponse = new ProposalResponse
                {
                    ReadWriteSet = new ReadWriteSet()
                },
                PrivateReadWriteSet = new ReadWriteSet()
            };
            var readWriteSetTransactionSerializerMock = new Mock<IReadWriteSetTransactionSerializer>();
            readWriteSetTransactionSerializerMock.Setup(x => x.Build(It.IsAny<ReadWriteSet>(), It.IsAny<ReadWriteSet>()))
                .Returns(proposalResponse);

            var tokenlessBroadcasterMock = new Mock<ITokenlessBroadcaster>();

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            var transientStore = new Mock<ITransientStore>();
            
            var organisationLookup = Mock.Of<IOrganisationLookup>();
            var endorsementValidator = Mock.Of<IEndorsementSignatureValidator>();

            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorFactoryMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
                stateRootMock.Object,
                readWriteSetTransactionSerializerMock.Object,
                new Endorsements(organisationLookup, endorsementValidator),
                transientStore.Object,
                tokenlessBroadcasterMock.Object,
                loggerFactoryMock.Object
                );

            Transaction transaction = this.network.CreateTransaction();
            var contractTxData = new ContractTxData(0, 0, (Gas)0, uint160.One, "CallMe");
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var mockPeer = new Mock<INetworkPeer>();

            var request = new EndorsementRequest
            {
                Peer = mockPeer.Object,
                ContractTransaction = transaction
            };

            Assert.True(await endorsementRequestHandler.ExecuteAndReturnProposalAsync(request));

            executorMock.Verify(x => x.Execute(It.Is<ContractTransactionContext>(y =>
                  y.TxIndex == 0 && y.BlockHeight == height + 1 && y.CoinbaseAddress == uint160.Zero && y.Sender == sender && y.TransactionHash == transaction.GetHash())));

            mockPeer.Verify(i => i.SendMessageAsync(It.IsAny<EndorsementPayload>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task ValidationFailsReturnsFalseAsync()
        {
            var validatorMock = new Mock<IEndorsementRequestValidator>();
            validatorMock.Setup(x => x.ValidateRequest(It.IsAny<EndorsementRequest>()))
                .Returns(false);

            var signerMock = new Mock<IEndorsementSigner>();

            var stateRootMock = new Mock<IStateRepositoryRoot>();

            var readWriteSetTransactionSerializerMock = new Mock<IReadWriteSetTransactionSerializer>();
            readWriteSetTransactionSerializerMock.Setup(x => x.Build(It.IsAny<ReadWriteSet>(), It.IsAny<ReadWriteSet>()))
                .Returns((SignedProposalResponse)null);

            var executorFactoryMock = new Mock<IContractExecutorFactory>();

            var tokenlessSignerMock = new Mock<ITokenlessSigner>();

            var consensusManagerMock = new Mock<IConsensusManager>();

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            var tokenlessBroadcasterMock = new Mock<ITokenlessBroadcaster>();

            var transientStore = new Mock<ITransientStore>();

            var organisationLookup = Mock.Of<IOrganisationLookup>();
            var endorsementValidator = Mock.Of<IEndorsementSignatureValidator>();

            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorFactoryMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
                stateRootMock.Object,
                readWriteSetTransactionSerializerMock.Object,
                new Endorsements(organisationLookup, endorsementValidator),
                transientStore.Object,
                tokenlessBroadcasterMock.Object,
                loggerFactoryMock.Object
                );

            Transaction transaction = this.network.CreateTransaction();
            var contractTxData = new ContractTxData(0, 0, (Gas)0, uint160.One, "CallMe");
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var request = new EndorsementRequest
            {
                ContractTransaction = transaction
            };

            Assert.False(await endorsementRequestHandler.ExecuteAndReturnProposalAsync(request));
        }
    }
}