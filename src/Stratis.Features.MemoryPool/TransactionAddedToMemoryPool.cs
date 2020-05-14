using NBitcoin;
using Stratis.Core.EventBus;

namespace Stratis.Features.MemoryPool
{
    /// <summary>
    /// Event that is executed when a transaction is removed from the mempool.
    /// </summary>
    /// <seealso cref="Stratis.Core.EventBus.EventBase" />
    public class TransactionAddedToMemoryPool : EventBase
    {
        public Transaction AddedTransaction { get; }

        public TransactionAddedToMemoryPool(Transaction addedTransaction)
        {
            this.AddedTransaction = addedTransaction;
        }
    }
}