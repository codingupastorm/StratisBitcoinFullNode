﻿using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
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

        // TODO: Invalid request should return false.


        [Fact]
        public void ExecutionSucceedsAndTransactionIsSigned()
        {
            var validatorMock = new Mock<IEndorsementRequestValidator>();
            validatorMock.Setup(x => x.ValidateRequest(It.IsAny<EndorsementRequest>()))
                .Returns(true);

            var signerMock = new Mock<IEndorsementSigner>();

            var executorMock = new Mock<IContractExecutor>();
            executorMock.Setup(x => x.Execute(It.IsAny<IContractTransactionContext>()))
                .Returns(new SmartContractExecutionResult
                {
                    Revert = false
                });

            var tokenlessSignerMock = new Mock<ITokenlessSigner>();
            tokenlessSignerMock.Setup(x => x.GetSender(It.IsAny<Transaction>()))
                .Returns(GetSenderResult.CreateSuccess(uint160.One));

            var consensusManagerMock = new Mock<IConsensusManager>();
            consensusManagerMock.Setup(x => x.Tip)
                .Returns(new ChainedHeader(new BlockHeader(), uint256.One, 0));

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(Mock.Of<ILogger>());

            var endorsementRequestHandler = new EndorsementRequestHandler(validatorMock.Object,
                signerMock.Object,
                executorMock.Object,
                tokenlessSignerMock.Object,
                consensusManagerMock.Object,
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

            // TODO: Verify that Executor is invoked with correct params.
        }
    }
}
