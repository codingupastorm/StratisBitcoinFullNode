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
            // Check that the block cointaining the transaction (if any) does not already occur on the active chain.
            uint256 blockId = this.blockStoreQueue.GetBlockIdByTransactionId(context.TransactionHash);
            if (blockId != null)
            {
                bool exists = this.chainIndexer.GetHeader(blockId) != null;
                if (exists)
                {
                    this.logger.LogDebug("'{0}' already exists.", context.Transaction.GetHash());
                    context.State.Fail(new MempoolError(MempoolErrors.RejectDuplicate, "duplicate-transaction-already-exists-on-chain"), $"'{context.Transaction.GetHash()}' already exists on chain.").Throw();
                }
            }
        }
    }
}
