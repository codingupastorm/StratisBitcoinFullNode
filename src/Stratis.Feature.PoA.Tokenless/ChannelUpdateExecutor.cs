﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Signals;
using Stratis.Core.EventBus;
using Stratis.Core.EventBus.CoreEvents;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Networks;

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
        private readonly IChannelRepository channelRepository;
        private readonly IChannelRequestSerializer channelRequestSerializer;
        private readonly ILogger<ChannelUpdateExecutor> logger;
        private readonly ISignals signals;
        private readonly Network network;

        private SubscriptionToken blockConnectedSubscription;

        public ChannelUpdateExecutor(
            ChannelSettings channelSettings,
            ILoggerFactory loggerFactory,
            IChannelService channelService,
            IChannelRepository channelRepository,
            IChannelRequestSerializer channelRequestSerializer,
            ISignals signals,
            Network network)
        {
            this.channelSettings = channelSettings;
            this.channelService = channelService;
            this.channelRepository = channelRepository;
            this.channelRequestSerializer = channelRequestSerializer;
            this.signals = signals;
            this.logger = loggerFactory.CreateLogger<ChannelUpdateExecutor>();
            this.network = network;
        }

        public void Initialize()
        {
            // TODO: Do Disconnected too
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
                    ChannelDefinition channelDef = this.channelRepository.GetChannelDefinition((this.network as ChannelNetwork).Name);

                    // Remove any members from the Remove pile
                    foreach (var orgToRemove in request.MembersToRemove.Organisations)
                    {
                        channelDef.AccessList.Organisations.Remove(orgToRemove);
                    }

                    foreach (var member in request.MembersToRemove.Thumbprints)
                    {
                        channelDef.AccessList.Thumbprints.Remove(member);
                    }

                    // Add those from the Add pile
                    foreach (var orgToAdd in request.MembersToAdd.Organisations)
                    {
                        // Could use another data structure?
                        if (!channelDef.AccessList.Organisations.Contains(orgToAdd))
                        {
                            channelDef.AccessList.Organisations.Add(orgToAdd);
                        }
                    }

                    foreach (var member in request.MembersToAdd.Thumbprints)
                    {
                        if (!channelDef.AccessList.Thumbprints.Contains(member))
                        {
                            channelDef.AccessList.Thumbprints.Add(member);
                        }
                    }
                }
            }
        }
    }
}