using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus.Rules;
using Stratis.Features.BlockStore;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Ensures that the chain does not already contain the given transaction in a block.
    /// </summary>
    public sealed class NoDuplicateTransactionExistOnChainRule : FullValidationConsensusRule
    {
        private readonly IBlockRepository blockRepository;
        private readonly ILogger<NoDuplicateTransactionExistOnChainRule> logger;
        private readonly ChainIndexer chainIndexer;

        public NoDuplicateTransactionExistOnChainRule(IBlockRepository blockRepository, ILoggerFactory loggerFactory, ChainIndexer chainIndexer)
        {
            this.blockRepository = blockRepository;
            this.logger = loggerFactory.CreateLogger<NoDuplicateTransactionExistOnChainRule>();
            this.chainIndexer = chainIndexer;
        }

        /// <inheritdoc/>
        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                uint256 txId = transaction.GetHash();

                uint256 blockId = this.blockRepository.GetBlockIdByTransactionId(txId);

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

