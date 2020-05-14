using NBitcoin;
using Stratis.Core.EventBus;

namespace Stratis.Features.MemoryPool
{
    /// <summary>
    /// Event that is executed when a transaction is removed from the mempool.
    /// </summary>
    /// <seealso cref="EventBase" />
    public class TransactionRemovedFromMemoryPool : EventBase
    {
        public Transaction RemovedTransaction { get; }

        public TransactionRemovedFromMemoryPool(Transaction removedTransaction)
        {
            this.RemovedTransaction = removedTransaction;
        }
    }
}
