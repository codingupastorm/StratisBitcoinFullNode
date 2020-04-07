using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Checks whether the transaction contains a <see cref=""/>
    /// </summary>
    public sealed class IsTransactionChannelCreationRequest : MempoolRule
    {
        private readonly IChannelService channelService;
        private readonly IChannelRequestSerializer channelRequestSerializer;

        public IsTransactionChannelCreationRequest(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            IChannelService channelService,
            IChannelRequestSerializer channelRequestSerializer)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.channelService = channelService;
            this.channelRequestSerializer = channelRequestSerializer;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // If the TxOut is null then this transaction does not contain any channel update execution code.
            TxOut scTxOut = context.Transaction.TryGetChannelUpdateTxOut();
            if (scTxOut == null)
                return;

            if (scTxOut.ScriptPubKey.IsChannelCreationRequest())
            {
                ChannelCreationRequest channelCreationRequest = this.channelRequestSerializer.Deserialize<ChannelCreationRequest>(scTxOut.ScriptPubKey);
                this.channelService.StartChannelNodeAsync(channelCreationRequest);
            }
        }
    }
}
