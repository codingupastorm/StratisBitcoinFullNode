using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Checks that the sender can be retrieved from the signature in the <see cref="TxIn"></see> as well as checking that they have the required CA role for CREATE or CALL.
    /// </summary>
    public sealed class CanSenderBeRetrievedPartialValidationRule : PartialValidationConsensusRule
    {
        private readonly ITokenlessSigner tokenlessSigner;

        public CanSenderBeRetrievedPartialValidationRule(ITokenlessSigner tokenlessSigner)
        {
            this.tokenlessSigner = tokenlessSigner;
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
                if (!getSenderResult.Success)
                    new ConsensusError("error-getting-sender", string.Format("Unable to determine the sender for transaction '{0}'.", transaction.GetHash())).Throw();
            }

            return Task.CompletedTask;
        }
    }
}
