using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Ensures that a channel creation request is well formed before passing it to consensus.
    /// </summary>
    public sealed class IsChannelCreationRequestWellFormed : MempoolRule
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;

        public IsChannelCreationRequestWellFormed(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ChannelSettings channelSettings,
            IChannelRequestSerializer channelRequestSerializer,
            ITokenlessSigner tokenlessSigner,
            ICertificatePermissionsChecker certificatePermissionsChecker)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.channelSettings = channelSettings;
            this.channelRequestSerializer = channelRequestSerializer;
            this.tokenlessSigner = tokenlessSigner;
            this.certificatePermissionsChecker = certificatePermissionsChecker;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // This rule is only applicable if this node is a system channel node.
            if (!this.channelSettings.IsSystemChannelNode)
            {
                this.logger.LogDebug($"This is not a system channel node.");
                return;
            }

            Transaction transaction = context.Transaction;

            // If the TxOut is null then this transaction does not contain any channel update execution code.
            TxOut txOut = transaction.TryGetChannelCreationRequestTxOut();
            if (txOut == null)
            {
                this.logger.LogDebug($"{transaction.GetHash()}' does not contain a channel creation request.");
                return;
            }

            (ChannelCreationRequest channelCreationRequest, string message) = this.channelRequestSerializer.Deserialize<ChannelCreationRequest>(txOut.ScriptPubKey);
            if (channelCreationRequest == null)
            {
                var errorMessage = $"Transaction '{transaction.GetHash()}' contained a channel creation request but its contents was malformed: {message}";

                this.logger.LogDebug(errorMessage);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-creation-request-malformed"), errorMessage).Throw();
            }

            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(transaction);
            if (!getSenderResult.Success)
            {
                var errorMessage = $"Unable to determine the sender for transaction '{transaction.GetHash()}'.";

                this.logger.LogDebug(errorMessage);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-creation-request-malformed"), errorMessage).Throw();
            }

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(transaction))
            {
                var errorMessage = $"The signature for transaction {transaction.GetHash()} is invalid.";

                this.logger.LogDebug(errorMessage);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-creation-request-malformed"), errorMessage).Throw();
            }

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel creation request with a valid signature.");

            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender, CaCertificatesManager.ChannelCreatePermissionOid))
            {
                var errorMessage = $"The sender of this transaction does not have the '{CaCertificatesManager.ChannelCreatePermission}' permission.";

                this.logger.LogDebug(errorMessage);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-creation-request-malformed"), errorMessage).Throw();
            }

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel creation request from a sender with a valid permission.");
        }
    }
}