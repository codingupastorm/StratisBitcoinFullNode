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
    /// Ensures that a channel update request is well formed before passing it to consensus.
    /// </summary>
    public sealed class IsChannelUpdateRequestWellFormed : MempoolRule
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;

        public IsChannelUpdateRequestWellFormed(
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
            Transaction transaction = context.Transaction;

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
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-update-request-malformed"), errorMessage).Throw();
            }

            // We need to check that the transaction is in the correct format (can we get the sender?)
            // and verify that the sender given is indeed the one who signed the transaction.
            GetSenderResult getSenderAndVerifyResult = this.tokenlessSigner.GetSenderAndVerify(transaction);
            if (!getSenderAndVerifyResult.Success)
            {
                this.logger.LogDebug(getSenderAndVerifyResult.Error);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-update-request-malformed"), getSenderAndVerifyResult.Error).Throw();
            }

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel update request with a valid signature.");

            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderAndVerifyResult.Sender, CaCertificatesManager.ChannelCreatePermissionOid))
            {
                var errorMessage = $"The sender of this transaction does not have the '{CaCertificatesManager.ChannelCreatePermission}' permission.";

                this.logger.LogDebug(errorMessage);
                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-update-request-malformed"), errorMessage).Throw();
            }

            this.logger.LogDebug($"{transaction.GetHash()}' contains a channel update request from a sender with a valid permission.");
        }
    }
}