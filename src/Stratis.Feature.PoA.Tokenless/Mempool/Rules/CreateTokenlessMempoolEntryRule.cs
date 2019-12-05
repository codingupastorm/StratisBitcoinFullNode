using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

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

        public override void Execute(MempoolValidationContext context)
        {
            // TODO-TL: Create the mempool entry in the validation context.

            // Set TxSize and Set TxMempoolEntry
        }
    }
}
