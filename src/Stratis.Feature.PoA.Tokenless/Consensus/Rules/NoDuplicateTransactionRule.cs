using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    public class NoDuplicateTransactionRule : FullValidationConsensusRule
    {
        private readonly IBlockRepository blockRepository;

        public NoDuplicateTransactionRule(IBlockRepository blockRepository, ILoggerFactory loggerFactory)
        {
            this.blockRepository = blockRepository;
        }

        public override Task RunAsync(RuleContext context)
        {
            uint256[] hashes = context.ValidationContext.BlockToValidate.Transactions.Select(x => x.GetHash()).ToArray();
            bool[] transactionsExist = this.blockRepository.TransactionsExist(hashes.ToArray());

            if (transactionsExist.Any())
            {
                // TODO: Might be nice to log the specific transaction that failed but it's a lot more code.
                TokenlessPoAConsensusErrors.DuplicateTransaction.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
