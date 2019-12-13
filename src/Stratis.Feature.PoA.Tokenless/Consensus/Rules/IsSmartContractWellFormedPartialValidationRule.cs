using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
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
