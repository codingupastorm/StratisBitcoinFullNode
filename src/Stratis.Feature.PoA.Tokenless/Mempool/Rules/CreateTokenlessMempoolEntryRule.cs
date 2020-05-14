using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Creates an entry in the <see cref="MempoolValidationContext"/>.
    /// </summary>
    public sealed class CreateTokenlessMempoolEntryRule : MempoolRule
    {
        public CreateTokenlessMempoolEntryRule(Network network, ITxMempool mempool, MempoolSettings settings, ChainIndexer chainIndexer, ILoggerFactory loggerFactory) : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            context.Entry = new TokenlessMempoolEntry(this.network.Consensus.Options, this.chainIndexer.Height, context.State.AcceptTime, context.Transaction);
            context.EntrySize = (int)context.Entry.GetTxSize();
        }
    }
}
