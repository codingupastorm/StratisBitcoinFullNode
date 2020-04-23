using System.Linq;
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
    public sealed class SenderInputPartialValidationRule : PartialValidationConsensusRule
    {
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly ITokenlessSigner tokenlessSigner;

        public SenderInputPartialValidationRule(
            ICertificatePermissionsChecker certificatePermissionsChecker,
            ITokenlessSigner tokenlessSigner)
        {
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.tokenlessSigner = tokenlessSigner;
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions.Where(x => !x.IsCoinBase))
            {
                this.ValidateTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        private void ValidateTransaction(Transaction transaction)
        {
            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
            if (!getSenderResult.Success)
                new ConsensusError("error-getting-sender", string.Format("Unable to determine the sender for transaction '{0}'.", transaction.GetHash())).Throw();

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(transaction))
                new ConsensusError("error-signature-invalid", $"The signature for transaction {transaction.GetHash()} is invalid.").Throw();

            // Now that we have the sender address, lets get their certificate.
            // Note that we can do for other permissions too. Contract permissions etc.

        }
    }
}
