﻿using Moq;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Features.SmartContracts.PoA;
using Stratis.Features.SmartContracts.PoA.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Consensus.Rules
{
    public class AllowedCodeHashLogicTests
    {
        private readonly Mock<IWhitelistedHashChecker> hashChecker;
        private readonly Mock<IContractCodeHashingStrategy> hashingStrategy;

        public AllowedCodeHashLogicTests()
        {
            this.hashChecker = new Mock<IWhitelistedHashChecker>();
            this.hashingStrategy = new Mock<IContractCodeHashingStrategy>();
        }

        [Fact]
        public void Should_Allow_Code_With_Valid_Hash()
        {
            var code = RandomUtils.GetBytes(2048);

            byte[] hash = HashHelper.Keccak256(code);

            this.hashingStrategy.Setup(h => h.Hash(code)).Returns(hash);
            this.hashChecker.Setup(h => h.CheckHashWhitelisted(hash)).Returns(true);

            var tx = new ContractTxData(1, 1000, (Gas)10000, code, new EndorsementPolicy());

            var sut = new AllowedCodeHashLogic(this.hashChecker.Object, this.hashingStrategy.Object);

            sut.CheckContractTransaction(tx, 0);

            this.hashChecker.Verify(h => h.CheckHashWhitelisted(hash), Times.Once);
        }

        [Fact]
        public void Should_Throw_ConsensusErrorException_If_Hash_Not_Allowed()
        {
            var code = RandomUtils.GetBytes(2048);

            byte[] hash = HashHelper.Keccak256(code);

            this.hashingStrategy.Setup(h => h.Hash(code)).Returns(hash);
            this.hashChecker.Setup(h => h.CheckHashWhitelisted(hash)).Returns(false);

            var sut = new AllowedCodeHashLogic(this.hashChecker.Object, this.hashingStrategy.Object);

            var tx = new ContractTxData(1, 1000, (Gas)10000, code, new EndorsementPolicy());

            Assert.Throws<ConsensusErrorException>(() => sut.CheckContractTransaction(tx, 0));

            this.hashChecker.Verify(h => h.CheckHashWhitelisted(hash), Times.Once);
        }

        [Fact]
        public void Should_Not_Validate_ContractCall()
        {
            var callTx = new ContractTxData(1, 1000, (Gas)10000, uint160.Zero, "Test");

            var sut = new AllowedCodeHashLogic(this.hashChecker.Object, this.hashingStrategy.Object);

            sut.CheckContractTransaction(callTx, 0);

            this.hashingStrategy.Verify(h => h.Hash(It.IsAny<byte[]>()), Times.Never);
            this.hashChecker.Verify(h => h.CheckHashWhitelisted(It.IsAny<byte[]>()), Times.Never);
        }
    }
}
