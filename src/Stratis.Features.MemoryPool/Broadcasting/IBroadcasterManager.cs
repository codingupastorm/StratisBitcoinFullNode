using System;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Features.MemoryPool.Broadcasting
{
    public interface IBroadcasterManager
    {
        Task BroadcastTransactionAsync(Transaction transaction);

        event EventHandler<TransactionBroadcastEntry> TransactionStateChanged;

        TransactionBroadcastEntry GetTransaction(uint256 transactionHash);

        void AddOrUpdate(Transaction transaction, State state, MempoolError mempoolError = null);
    }
}
