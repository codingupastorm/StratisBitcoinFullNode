using System;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Signals;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelCreationExecutor
    {
        void Initialize();
    }

    /// <summary>
    /// Executes a channel creation request if the transaction contains it.
    /// </summary>
    public sealed class ChannelCreationExecutor : IChannelCreationExecutor, IDisposable
    {
        private readonly ILogger<ChannelCreationExecutor> logger;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ChannelSettings channelSettings;
        private readonly IChannelService channelService;
        private readonly ISignals signals;

        private SubscriptionToken blockConnectedSubscription;

        public ChannelCreationExecutor(
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
            this.logger = loggerFactory.CreateLogger<ChannelCreationExecutor>();
        }

        public void Initialize()
        {
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
        }

        private void OnBlockConnected(BlockConnected blockConnectedEvent)
        {
            if (!this.channelSettings.IsSystemChannelNode)
            {
                this.logger.LogDebug($"Only system channel nodes can process channel creation requests.");
                return;
            }

            foreach (Transaction transaction in blockConnectedEvent.ConnectedBlock.Block.Transactions)
            {
                // If the TxOut is null then this transaction does not contain any channel creation execution code.
                TxOut txOut = transaction.TryGetChannelCreationRequestTxOut();
                if (txOut == null)
                {
                    this.logger.LogDebug($"{transaction.GetHash()}' does not contain a channel creation request.");
                    continue;
                }

                (ChannelCreationRequest channelCreationRequest, string message) = this.channelRequestSerializer.Deserialize<ChannelCreationRequest>(txOut.ScriptPubKey);
                if (channelCreationRequest != null)
                {
                    this.logger.LogDebug("Transaction '{0}' contains a request to create channel '{1}'.", transaction.GetHash(), channelCreationRequest.Name);
                    this.channelService.CreateAndStartChannelNodeAsync(channelCreationRequest).GetAwaiter().GetResult();
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.signals.Unsubscribe(this.blockConnectedSubscription);
        }
    }
}
