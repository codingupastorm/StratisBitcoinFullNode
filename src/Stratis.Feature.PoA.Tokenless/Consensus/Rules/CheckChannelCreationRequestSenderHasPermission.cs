using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Checks that the sender of the create channel request transaction has the required permission.
    /// </summary>
    public sealed class CheckChannelCreationRequestSenderHasPermission : PartialValidationConsensusRule
    {
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly ILogger<CheckChannelCreationRequestSenderHasPermission> logger;
        private readonly ITokenlessSigner tokenlessSigner;

        public CheckChannelCreationRequestSenderHasPermission(
            ICertificatePermissionsChecker certificatePermissionsChecker,
            ILoggerFactory loggerFactory,
            ITokenlessSigner tokenlessSigner)
        {
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.logger = loggerFactory.CreateLogger<CheckChannelCreationRequestSenderHasPermission>();
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
            // If the TxOut is null then this transaction does not contain any channel create execution code.
            TxOut txOut = transaction.TryGetChannelCreationRequestTxOut();
            if (txOut == null)
            {
                this.logger.LogDebug($"{transaction.GetHash()}' does not contain a channel creation request.");
                return;
            }

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel creation request.");

            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
            if (!getSenderResult.Success)
                new ConsensusError("error-getting-sender", string.Format("Unable to determine the sender for transaction '{0}'.", transaction.GetHash())).Throw();

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(transaction))
                new ConsensusError("error-signature-invalid", $"The signature for transaction {transaction.GetHash()} is invalid.").Throw();

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel creation request with a valid signature.");

            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender, CaCertificatesManager.ChannelCreatePermissionOid))
                new ConsensusError("error-signature-invalid", $"The sender of this transaction does not have the '{CaCertificatesManager.ChannelCreatePermission}' permission.").Throw();

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel creation request from a sender with a valid permission.");
        }
    }
}
