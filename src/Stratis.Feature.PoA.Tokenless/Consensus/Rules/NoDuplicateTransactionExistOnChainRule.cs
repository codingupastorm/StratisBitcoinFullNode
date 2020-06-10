using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus.Rules;
using Stratis.Core.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Ensures that the chain does not already contain the given transaction in a block.
    /// </summary>
    public sealed class NoDuplicateTransactionExistOnChainRule : FullValidationConsensusRule
    {
        private readonly IBlockStoreQueue blockStoreQueue;
        private readonly ILogger<NoDuplicateTransactionExistOnChainRule> logger;
        private readonly ChainIndexer chainIndexer;

        public NoDuplicateTransactionExistOnChainRule(IBlockStoreQueue blockStoreQueue, ILoggerFactory loggerFactory, ChainIndexer chainIndexer)
        {
            this.blockStoreQueue = blockStoreQueue;
            this.logger = loggerFactory.CreateLogger<NoDuplicateTransactionExistOnChainRule>();
            this.chainIndexer = chainIndexer;
        }

        /// <inheritdoc/>
        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                // Check that the block cointaining the transaction (if any) does not already occur on the active chain.
                uint256 txId = transaction.GetHash();
                uint256 blockId = this.blockStoreQueue.GetBlockIdByTransactionId(txId);

                if (blockId != null)
                {
                    bool exists = this.chainIndexer.GetHeader(blockId) != null;
                    if (exists)
                    {
                        this.logger.LogDebug("'{0}' already exists.", txId);
                        TokenlessPoAConsensusErrors.DuplicateTransaction.Throw();
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}

