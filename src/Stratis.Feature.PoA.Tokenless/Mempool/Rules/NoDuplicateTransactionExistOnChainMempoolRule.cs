using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Interfaces;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Ensures that the chain does not already contain the given transaction in a block.
    /// </summary>
    public sealed class NoDuplicateTransactionExistOnChainMempoolRule : MempoolRule
    {
        private readonly IBlockStoreQueue blockStoreQueue;

        public NoDuplicateTransactionExistOnChainMempoolRule(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            IBlockStoreQueue blockStoreQueue)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.blockStoreQueue = blockStoreQueue;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            Transaction exists = this.blockStoreQueue.GetTransactionById(context.TransactionHash);
            if (exists != null)
            {
                this.logger.LogDebug("'{0}' already exists.", context.Transaction.GetHash());
                context.State.Fail(new MempoolError(MempoolErrors.RejectDuplicate, "duplicate-transaction-already-exists-on-chain"), $"'{context.Transaction.GetHash()}' already exists on chain.").Throw();
            }
        }
    }
}
