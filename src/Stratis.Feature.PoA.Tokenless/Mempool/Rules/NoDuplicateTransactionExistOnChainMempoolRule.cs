using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Ensures that the chain does not already contain the given transaction in a block.
    /// </summary>
    public sealed class NoDuplicateTransactionExistOnChainMempoolRule : MempoolRule
    {
        private readonly IBlockRepository blockRepository;

        public NoDuplicateTransactionExistOnChainMempoolRule(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            IBlockRepository blockRepository)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.blockRepository = blockRepository;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            bool exists = this.blockRepository.TransactionExists(context.TransactionHash);
            if (exists)
            {
                this.logger.LogDebug("'{0}' already exists.", context.Transaction.GetHash());
                context.State.Fail(new MempoolError(MempoolErrors.RejectDuplicate, "duplicate-transaction-already-exists-on-chain"), $"'{context.Transaction.GetHash()}' already exists on chain.").Throw();
            }
        }
    }
}
