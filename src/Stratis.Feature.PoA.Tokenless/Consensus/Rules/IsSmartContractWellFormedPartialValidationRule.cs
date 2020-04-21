using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public class OpReadWriteSetMustContainSignatures : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                for(var i = 0; i < transaction.Outputs.Count; i++)
                {
                    var output = transaction.Outputs[i];
                    if (!output.ScriptPubKey.IsReadWriteSet())
                        continue;

                    // No more outputs to check.
                    if (i + 1 == transaction.Outputs.Count)
                        new ConsensusError("badly-formed-rws", "An OP_READWRITE must be followed by an endorsement").Throw();

                    // TODO check that i + 1 is a signature format
                }
            }

            return Task.CompletedTask;
        }
    }
    /// <summary>
    /// Checks that smart contract transactions are in a valid format and the data is serialized correctly.
    /// </summary>
    public sealed class IsSmartContractWellFormedPartialValidationRule : PartialValidationConsensusRule
    {
        private readonly ICallDataSerializer callDataSerializer;

        public IsSmartContractWellFormedPartialValidationRule(ICallDataSerializer callDataSerializer)
        {
            this.callDataSerializer = callDataSerializer;
        }

        /// <inheritdoc/>
        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                // If the TxOut is null then this transaction does not contain any smart contract execution code.
                TxOut scTxOut = transaction.TryGetSmartContractTxOut();
                if (scTxOut == null)
                    continue;

                ContractTransactionChecker.GetContractTxData(this.callDataSerializer, scTxOut);
            }

            return Task.CompletedTask;
        }
    }
}
