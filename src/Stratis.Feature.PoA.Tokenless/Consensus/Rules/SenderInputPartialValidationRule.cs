using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Channels;
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
        private readonly Network network;

        public SenderInputPartialValidationRule(
            ICertificatePermissionsChecker certificatePermissionsChecker,
            ITokenlessSigner tokenlessSigner,
            Network network)
        {
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.tokenlessSigner = tokenlessSigner;
            this.network = network;
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

            // If we're not on the OG network, then check that the sender is allowed to be on this network.
            // TODO: Is this check for the current channel robust?
            if (this.network is ChannelNetwork channelNetwork && channelNetwork.Id != ChannelService.SystemChannelId)
            {
                if (!this.certificatePermissionsChecker.CheckSenderCertificateIsPermittedOnChannel(getSenderResult.Sender, channelNetwork))
                {
                    new ConsensusError("error-illegal-sender", $"The sender of this transaction {transaction.GetHash()} isn't permitted on this network.").Throw();
                }
            }

            // TODO: This is missing things that the SenderInputMempoolRule has
        }
    }
}
