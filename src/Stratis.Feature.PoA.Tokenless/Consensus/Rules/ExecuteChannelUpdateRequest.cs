
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Executes a channel update request if the transaction contains it.
    /// </summary>
    public sealed class ExecuteChannelUpdateRequest : FullValidationConsensusRule
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelService channelService;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ILogger<ExecuteChannelCreationRequest> logger;

        public ExecuteChannelUpdateRequest(
            ChannelSettings channelSettings,
            ILoggerFactory loggerFactory,
            IChannelService channelService,
            IChannelRequestSerializer channelRequestSerializer)
        {
            this.channelSettings = channelSettings;
            this.channelService = channelService;
            this.channelRequestSerializer = channelRequestSerializer;
            this.logger = loggerFactory.CreateLogger<ExecuteChannelCreationRequest>();
        }

        /// <inheritdoc/>
        public override async Task RunAsync(RuleContext context)
        {
            // This rule is only applicable if this node is a system channel node.
            if (!this.channelSettings.IsSystemChannelNode)
            {
                this.logger.LogDebug($"This is not a system channel node.");
                return;
            }

            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                // If the TxOut is null then this transaction does not contain any channel update execution code.
                TxOut txOut = transaction.TryGetChannelUpdateRequestTxOut();
                if (txOut == null)
                {
                    this.logger.LogDebug($"{transaction.GetHash()}' does not contain a channel update request.");
                    continue;
                }

                (ChannelUpdateRequest request, string message) = this.channelRequestSerializer.Deserialize<ChannelUpdateRequest>(txOut.ScriptPubKey);
                if (request != null)
                {
                    this.logger.LogDebug("Transaction '{0}' contains a request to update channel '{1}'.", transaction.GetHash(), request.Name);

                    // TODO: Implement update to channel definitions etc.
                }
            }
        }
    }
}
