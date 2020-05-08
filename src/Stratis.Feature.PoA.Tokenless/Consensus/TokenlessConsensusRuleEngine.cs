using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Base;
using Stratis.Core.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Features.Consensus;
using Stratis.Core.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    /// <inheritdoc />
    public sealed class TokenlessConsensusRuleEngine : ConsensusRuleEngine
    {
        public TokenlessConsensusRuleEngine(ChainIndexer chainIndexer, IChainState chainState, ICheckpoints checkpoints,
            ConsensusRulesContainer consensusRulesContainer, ConsensusSettings consensusSettings, IDateTimeProvider dateTimeProvider,
            IInvalidBlockHashStore invalidBlockHashStore, ILoggerFactory loggerFactory, Network network, NodeDeployments nodeDeployments,
            INodeStats nodeStats)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, chainState, invalidBlockHashStore, nodeStats, consensusRulesContainer)
        {
        }

        /// <summary>
        /// This gets overridden in a tokenless network as we can't return coinview's tip as it does not apply.
        /// </summary>
        /// <returns>The <see cref="ChainIndexer"/>'s tip.</returns>
        public override uint256 GetBlockHash()
        {
            return base.ChainIndexer.Tip.HashBlock;
        }

        public override void ConsensusSpecificTxChecks(Transaction tx)
        {
            throw new NotImplementedException();
        }

        public override void ConsensusSpecificRequiredTxChecks(Transaction tx)
        {
            throw new NotImplementedException();
        }

        public override RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return new PowRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        public override Task<RewindState> RewindAsync()
        {
            throw new NotImplementedException();
        }
    }
}
