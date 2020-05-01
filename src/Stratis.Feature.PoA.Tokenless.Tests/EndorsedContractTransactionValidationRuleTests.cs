using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsedContractTransactionValidationRuleTests
    {
        private readonly Mock<IEndorsedTransactionBuilder> builder;
        private readonly Mock<IEndorsementSignatureValidator> validator;

        public EndorsedContractTransactionValidationRuleTests()
        {
            this.builder = new Mock<IEndorsedTransactionBuilder>();
            this.validator = new Mock<IEndorsementSignatureValidator>();
        }

        [Fact]
        public void Transaction_Is_Not_ReadWriteSet()
        {
            var rule = new EndorsedContractTransactionValidationRule(this.builder.Object, this.validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) ScOpcodeType.OP_CALLCONTRACT
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.InvalidCall, result.Item2);
            
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;
            this.builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Never);
            this.validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Missing_Data_Is_Malformed()
        {
            var rule = new EndorsedContractTransactionValidationRule(this.builder.Object, this.validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            // Tx says it has a RWS but is otherwise malformed because it's missing data.
            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, result.Item2);

            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;
            this.builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            this.validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Bad_Data_Is_Malformed()
        {
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;

            // Returns false = malformed
            this.builder.Setup(b => b.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws)).Returns(false);

            var rule = new EndorsedContractTransactionValidationRule(this.builder.Object, this.validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, result.Item2);

            this.builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            this.validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Good_Data_Is_Not_Malformed_Signatures_Invalid()
        {
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;

            // Not malformed
            this.builder.Setup(b => b.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws)).Returns(true);

            // Signatures invalid.
            this.validator.Setup(b => b.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>())).Returns(false);

            var rule = new EndorsedContractTransactionValidationRule(this.builder.Object, this.validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid, result.Item2);

            this.builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            this.validator.Verify(x => x.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>()), Times.Once);
        }

        [Fact]
        public void Transaction_Good_Data_Is_Not_Malformed_Signatures_Valid()
        {
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;

            // Not malformed
            this.builder.Setup(b => b.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws)).Returns(true);
            
            // Signatures valid.
            this.validator.Setup(b => b.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>())).Returns(true);

            var rule = new EndorsedContractTransactionValidationRule(this.builder.Object, this.validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.True(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.None, result.Item2);

            this.builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            this.validator.Verify(x => x.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>()), Times.Once);
        }
    }
}