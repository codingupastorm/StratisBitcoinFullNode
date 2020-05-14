using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Signals;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IChannelUpdateExecutor
    {
        void Initialize();
    }

    /// <summary>
    /// Executes a channel update request if the transaction contains it.
    /// </summary>
    public sealed class ChannelUpdateExecutor : IChannelUpdateExecutor
    {
        private readonly ChannelSettings channelSettings;
        private readonly IChannelService channelService;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ILogger<ChannelUpdateExecutor> logger;
        private readonly ISignals signals;

        private SubscriptionToken blockConnectedSubscription;

        public ChannelUpdateExecutor(
            ChannelSettings channelSettings,
            ILoggerFactory loggerFactory,
            IChannelService channelService,
            IChannelRequestSerializer channelRequestSerializer,
            ISignals signals)
        {
            this.channelSettings = channelSettings;
            this.channelService = channelService;
            this.channelRequestSerializer = channelRequestSerializer;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger<ChannelUpdateExecutor>();
        }

        public void Initialize()
        {
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
        }

        /// <inheritdoc/>
        private void OnBlockConnected(BlockConnected blockConnectedEvent)
        {
            // This rule is only applicable if this node is a system channel node.
            if (!this.channelSettings.IsSystemChannelNode)
            {
                this.logger.LogDebug($"Only system channel nodes can process channel update requests.");
                return;
            }

            foreach (Transaction transaction in blockConnectedEvent.ConnectedBlock.Block.Transactions)
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

                    // Get channel membership

                    // Remove any members from the Remove pile

                    // Add any members from the Add pile

                    // Save channel membership

                    throw new NotImplementedException("See comments above.");
                }
            }
        }
    }
}
