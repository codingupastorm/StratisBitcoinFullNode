using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public sealed class NoDuplicateTransactionRule : FullValidationConsensusRule
    {
        private readonly IBlockRepository blockRepository;
        private readonly ILogger<NoDuplicateTransactionRule> logger;

        public NoDuplicateTransactionRule(IBlockRepository blockRepository, ILoggerFactory loggerFactory)
        {
            this.blockRepository = blockRepository;
            this.logger = loggerFactory.CreateLogger<NoDuplicateTransactionRule>();
        }

        public override Task RunAsync(RuleContext context)
        {
            foreach (Transaction transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                uint256 txId = transaction.GetHash();

                bool exists = this.blockRepository.TransactionExist(txId);
                if (exists)
                {
                    this.logger.LogDebug("'{0}' already exists.", txId);
                    TokenlessPoAConsensusErrors.DuplicateTransaction.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}
