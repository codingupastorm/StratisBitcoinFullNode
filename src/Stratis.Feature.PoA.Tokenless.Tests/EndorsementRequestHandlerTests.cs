using Microsoft.Extensions.Logging;
using Moq;
using Stratis.Bitcoin.Consensus;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementRequestHandlerTests
    {

        [Fact]
        public void ExecutionSucceedsAndTransactionIsSigned()
        {
            Mock<IEndorsementRequestValidator> validatorMock = new Mock<IEndorsementRequestValidator>();
            Mock<IEndorsementSigner> signerMock = new Mock<IEndorsementSigner>();
            Mock<IContractExecutor> executorMock = new Mock<IContractExecutor>();
            Mock<ITokenlessSigner> tokenlessSignerMock = new Mock<ITokenlessSigner>();
            Mock<IConsensusManager> consensusManagerMock = new Mock<IConsensusManager>();
            Mock<ILoggerFactory> loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());


            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
                loggerFactoryMock.Object
                );
        }
    }
}
