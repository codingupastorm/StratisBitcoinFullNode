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
        [Fact]
        public void Transaction_Is_Not_ReadWriteSet()
        {
            var builder = new Mock<IEndorsedTransactionBuilder>();
            var validator = new Mock<IEndorsementValidator>();

            var rule = new EndorsedContractTransactionValidationRule(builder.Object, validator.Object);

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
            builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Never);
            validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Missing_Data_Is_Malformed()
        {
            var builder = new Mock<IEndorsedTransactionBuilder>();
            var validator = new Mock<IEndorsementValidator>();

            var rule = new EndorsedContractTransactionValidationRule(builder.Object, validator.Object);

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
            builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Bad_Data_Is_Malformed()
        {
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;
            var builder = new Mock<IEndorsedTransactionBuilder>();

            // Returns false = malformed
            builder.Setup(b => b.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws)).Returns(false);

            var validator = new Mock<IEndorsementValidator>();

            var rule = new EndorsedContractTransactionValidationRule(builder.Object, validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.Malformed, result.Item2);

            builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            validator.Verify(x => x.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()), Times.Never);
        }

        [Fact]
        public void Transaction_Good_Data_Is_Not_Malformed_Signatures_Invalid()
        {
            IEnumerable<Endorsement.Endorsement> endorsements;
            ReadWriteSet rws;
            var builder = new Mock<IEndorsedTransactionBuilder>();

            // Not malformed
            builder.Setup(b => b.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws)).Returns(true);

            var validator = new Mock<IEndorsementValidator>();

            // Signatures invalid.
            validator.Setup(b => b.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>())).Returns(false);

            var rule = new EndorsedContractTransactionValidationRule(builder.Object, validator.Object);

            var tx = new Transaction();
            var ops = new List<byte>
            {
                (byte) OpcodeType.OP_READWRITE
            };

            tx.Outputs.Add(new TxOut(Money.Zero, new Script(ops)));

            var result = rule.CheckTransaction(tx);

            Assert.False(result.Item1);
            Assert.Equal(EndorsedContractTransactionValidationRule.EndorsementValidationErrorType.SignaturesInvalid, result.Item2);

            builder.Verify(x => x.TryParseTransaction(It.IsAny<Transaction>(), out endorsements, out rws), Times.Once);
            validator.Verify(x => x.Validate(It.IsAny<IEnumerable<Endorsement.Endorsement>>(), It.IsAny<byte[]>()), Times.Once);
        }
    }
}