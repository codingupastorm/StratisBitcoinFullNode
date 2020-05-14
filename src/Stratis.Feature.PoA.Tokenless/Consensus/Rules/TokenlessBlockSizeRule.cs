using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Core.Consensus.Rules;
using Stratis.Core.Utilities.Extensions;
using Stratis.Features.Consensus.Rules.CommonRules;

namespace Stratis.Feature.PoA.Tokenless.Consensus.Rules
{
    /// <summary>
    /// Very similar to <see cref="BlockSizeRule"/> but only checks block size, and doesn't check the amount of transactions is > 0.
    /// </summary>
    public class TokenlessBlockSizeRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            if (context.SkipValidation)
                return Task.CompletedTask;

            var consensus = this.Parent.Network.Consensus;

            Block block = context.ValidationContext.BlockToValidate;

            // Size limits.
            if (
                (block.Transactions.Count > consensus.Options.MaxBlockBaseSize) ||
                (block.GetSize(TransactionOptions.None, this.Parent.Network.Consensus.ConsensusFactory) > consensus.Options.MaxBlockBaseSize))
            {
                this.Logger.LogTrace("(-)[BAD_BLOCK_LENGTH]");
                ConsensusErrors.BadBlockLength.Throw();
            }

            return Task.CompletedTask;
        }
    }
}
