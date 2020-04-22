using System.Linq;
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
                if (transaction.Outputs.Count == 0)
                    continue;
                
                var output = transaction.Outputs[0];
                if (!output.ScriptPubKey.IsReadWriteSet())
                    continue;

                // Must have 2 or more outputs if OP_RWS
                if(transaction.Outputs.Count < 2)
                    new ConsensusError("badly-formed-rws", "An OP_READWRITE must be followed by an endorsement").Throw();

                for (var i = 1; i < transaction.Outputs.Count; i++)
                {  
                    // Validate endorsement format
                    if(!ValidateEndorsement(transaction.Outputs[i].ScriptPubKey.ToBytes()))
                    {
                        new ConsensusError("badly-formed-endorsement", "Endorsement was not in the correct format.").Throw();
                    }
                }
            }

            return Task.CompletedTask;
        }

        private static bool ValidateEndorsement(byte[] toBytes)
        {
            try
            {
                Endorsement.Endorsement.FromBytes(toBytes);

                return true;
            }
            catch
            {
                return false;
            }
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
