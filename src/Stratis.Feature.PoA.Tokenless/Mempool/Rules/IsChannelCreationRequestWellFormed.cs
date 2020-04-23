using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Ensures that a channel creation request is well formed before passing it to consensus.
    /// </summary>
    public sealed class IsChannelCreationRequestWellFormed : MempoolRule
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelRequestSerializer channelRequestSerializer;

        public IsChannelCreationRequestWellFormed(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ChannelSettings channelSettings,
            IChannelRequestSerializer channelRequestSerializer)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.channelSettings = channelSettings;
            this.channelRequestSerializer = channelRequestSerializer;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // This rule is only applicable if this node is a system channel node.
            if (!this.channelSettings.IsSystemChannelNode)
                return;

            // If the TxOut is null then this transaction does not contain any channel update execution code.
            TxOut txOut = context.Transaction.TryGetChannelCreationRequestTxOut();
            if (txOut != null)
            {
                (ChannelCreationRequest channelCreationRequest, string message) = this.channelRequestSerializer.Deserialize<ChannelCreationRequest>(txOut.ScriptPubKey);
                if (channelCreationRequest == null)
                {
                    var errorMessage = $"Transaction '{context.Transaction.GetHash()}' contained a channel creation request but its contents was malformed: {message}";

                    this.logger.LogDebug(errorMessage);
                    context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-creation-request-malformed"), errorMessage).Throw();
                }
            }

            // If the TxOut is null then this transaction does not contain any channel update execution code.
            txOut = context.Transaction.TryGetChannelAddMemberRequestTxOut();
            if (txOut != null)
            {
                (ChannelAddMemberRequest channelAddMemberRequest, string message) = this.channelRequestSerializer.Deserialize<ChannelAddMemberRequest>(txOut.ScriptPubKey);
                if (channelAddMemberRequest == null)
                {
                    var errorMessage = $"Transaction '{context.Transaction.GetHash()}' contained a channel 'add member` request but its contents was malformed: {message}";

                    this.logger.LogDebug(errorMessage);
                    context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "channel-addmember-request-malformed"), errorMessage).Throw();
                }
            }
        }
    }
}