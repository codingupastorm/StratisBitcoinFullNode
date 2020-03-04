using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
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
        public void ExecutionSucceedsAndTransactionIsSigned()
        {
            const int height = 16;
            uint160 sender = uint160.One;

            var validatorMock = new Mock<IEndorsementRequestValidator>();
            validatorMock.Setup(x => x.ValidateRequest(It.IsAny<EndorsementRequest>()))
                .Returns(true);

            var signerMock = new Mock<IEndorsementSigner>();

            var stateRootMock = new Mock<IStateRepositoryRoot>();

            var executorMock = new Mock<IContractExecutor>();
            executorMock.Setup(x => x.Execute(It.IsAny<IContractTransactionContext>()))
                .Returns(new SmartContractExecutionResult
                {
                    Revert = false
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

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorFactoryMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
                stateRootMock.Object,
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

            Assert.True(endorsementRequestHandler.ExecuteAndSignProposal(request));

            executorMock.Verify(x=>x.Execute(It.Is<ContractTransactionContext>(y =>
                y.TxIndex == 0 && y.BlockHeight == height && y.CoinbaseAddress == uint160.Zero && y.Sender == sender && y.TransactionHash == transaction.GetHash())));
        }

        [Fact]
        public void ValidationFailsReturnsFalse()
        {
            var validatorMock = new Mock<IEndorsementRequestValidator>();
            validatorMock.Setup(x => x.ValidateRequest(It.IsAny<EndorsementRequest>()))
                .Returns(false);

            var signerMock = new Mock<IEndorsementSigner>();

            var stateRootMock = new Mock<IStateRepositoryRoot>();

            var executorFactoryMock = new Mock<IContractExecutorFactory>();

            var tokenlessSignerMock = new Mock<ITokenlessSigner>();

            var consensusManagerMock = new Mock<IConsensusManager>();

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorFactoryMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
                stateRootMock.Object,
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

            Assert.False(endorsementRequestHandler.ExecuteAndSignProposal(request));
        }
    }
}
