using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Checks that the sender can be retrieved from the signature in the <see cref="TxIn"></see> as well as checking that they have the required CA role for CREATE or CALL.
    /// </summary>
    public sealed class CanSmartContractSenderBeRetrievedPartialValidationRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            // TODO-TL: Implement

            return Task.CompletedTask;
        }
    }
}
