using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Core.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public sealed class CheckChannelUpdateRequestSenderHasPermission : PartialValidationConsensusRule
    {
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly ILogger<CheckChannelCreationRequestSenderHasPermission> logger;
        private readonly ITokenlessSigner tokenlessSigner;

        public CheckChannelUpdateRequestSenderHasPermission(
            IChannelRequestSerializer channelRequestSerializer,
            ICertificatePermissionsChecker certificatePermissionsChecker,
            ILoggerFactory loggerFactory,
            ITokenlessSigner tokenlessSigner)
        {
            this.channelRequestSerializer = channelRequestSerializer;
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
            // If the TxOut is null then this transaction does not contain any channel update execution code.
            TxOut txOut = transaction.TryGetChannelUpdateRequestTxOut();
            if (txOut == null)
            {
                this.logger.LogDebug($"{transaction.GetHash()}' does not contain a channel update request.");
                return;
            }

            (ChannelUpdateRequest request, string message) = this.channelRequestSerializer.Deserialize<ChannelUpdateRequest>(txOut.ScriptPubKey);
            if (request == null)
            {
                var errorMessage = $"Transaction '{transaction.GetHash()}' contained a channel update request but its contents was malformed: {message}";

                this.logger.LogDebug(errorMessage);
                new ConsensusError("channel-update-request-malformed", string.Format("Channel update request was malformed '{0}'.", transaction.GetHash())).Throw();
            }

            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
            if (!getSenderResult.Success)
                new ConsensusError("error-getting-sender", string.Format("Unable to determine the sender for transaction '{0}'.", transaction.GetHash())).Throw();

            if (!this.tokenlessSigner.Verify(transaction))
                new ConsensusError("error-signature-invalid", $"The signature for transaction {transaction.GetHash()} is invalid.").Throw();

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel update request with a valid signature.");

            // At the moment, channel create permission is the same as the channel update permission.
            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender, CaCertificatesManager.ChannelCreatePermissionOid))
                new ConsensusError("error-signature-invalid", $"The sender of this transaction does not have the '{CaCertificatesManager.ChannelCreatePermission}' permission.").Throw();

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel update request from a sender with a valid permission.");
        }
    }
}