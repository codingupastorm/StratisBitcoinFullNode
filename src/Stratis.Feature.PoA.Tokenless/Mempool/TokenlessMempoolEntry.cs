using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    public sealed class TokenlessMempoolEntry : TxMempoolEntry
    {
        public TokenlessMempoolEntry(ConsensusOptions consensusOptions, int entryHeight, long entryTime, Transaction transaction)
            : base(consensusOptions, entryHeight, entryTime, transaction)
        {
        }
    }
}
