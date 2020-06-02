using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Core.Consensus;
using Stratis.Core.Networks;
using Stratis.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosBlockSignatureRepresentationRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private readonly Key key;
        private readonly Network stratisMain = new StratisMain();

        public PosBlockSignatureRepresentationRuleTest()
        {
            this.key = new Key();
        }

        [Fact]
        public void Run_IsCanonicalBlockSignature_DoesNotThrowException()
        {
            Block block = this.stratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(this.stratisMain.CreateTransaction());

            Transaction transaction = this.stratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            // By default the Sign method will give a signature in canonical format.
            ECDSASignature signature = this.key.Sign(block.GetHash());

            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.BlockToValidate = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.BlockToValidate));

            this.consensusRules.RegisterRule<PosBlockSignatureRepresentationRule>().Run(this.ruleContext);
        }

        [Fact]
        public void Run_IsNotCanonicalBlockSignature_ThrowsBadBlockSignatureConsensusErrorException()
        {
            Block block = this.stratisMain.Consensus.ConsensusFactory.CreateBlock();
            block.Transactions.Add(this.stratisMain.CreateTransaction());

            Transaction transaction = this.stratisMain.CreateTransaction();
            transaction.Inputs.Add(new TxIn()
            {
                PrevOut = new OutPoint(new uint256(15), 1),
                ScriptSig = new Script()
            });

            transaction.Outputs.Add(new TxOut(Money.Zero, (IDestination)null));
            var scriptPubKeyOut = new Script(Op.GetPushOp(this.key.PubKey.ToBytes(true)), OpcodeType.OP_CHECKSIG);
            transaction.Outputs.Add(new TxOut(Money.Zero, scriptPubKeyOut));
            block.Transactions.Add(transaction);

            ECDSASignature signature = this.key.Sign(block.GetHash());

            signature = signature.MakeNonCanonical();

            // Ensure the non-canonical signature is still a valid signature for the block, just in the wrong format.
            Assert.True(this.key.PubKey.Verify(block.GetHash(), signature));
            Assert.False(signature.IsLowS);

            (block as PosBlock).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.ruleContext.ValidationContext.BlockToValidate = block;
            Assert.True(BlockStake.IsProofOfStake(this.ruleContext.ValidationContext.BlockToValidate));

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosBlockSignatureRepresentationRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadBlockSignature, exception.ConsensusError);
        }
    }
}
