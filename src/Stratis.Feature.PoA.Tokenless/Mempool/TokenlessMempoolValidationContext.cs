using NBitcoin;
using Stratis.Features.MemoryPool;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    public sealed class TokenlessMempoolValidationContext : MempoolValidationContext
    {
        public TokenlessMempoolValidationContext(Transaction transaction, MempoolValidationState mempoolValidationState)
            : base(transaction, mempoolValidationState)
        {
        }
    }
}
