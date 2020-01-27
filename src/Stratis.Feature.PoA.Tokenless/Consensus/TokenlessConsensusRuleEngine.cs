using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    /// <inheritdoc />
    public sealed class TokenlessConsensusRuleEngine : PowConsensusRuleEngine
    {
        public ISlotsManager SlotsManager { get; private set; }

        public PoABlockHeaderValidator PoaHeaderValidator { get; private set; }

        public IFederationManager FederationManager { get; private set; }

        public TokenlessConsensusRuleEngine(IAsyncProvider asyncProvider, ChainIndexer chainIndexer, IChainState chainState, ICheckpoints checkpoints,
            ICoinView utxoSet, ConsensusRulesContainer consensusRulesContainer, ConsensusSettings consensusSettings, IDateTimeProvider dateTimeProvider, IFederationManager federationManager,
            IInvalidBlockHashStore invalidBlockHashStore, ILoggerFactory loggerFactory, Network network, NodeDeployments nodeDeployments,
            INodeStats nodeStats, PoABlockHeaderValidator poaHeaderValidator, ISlotsManager slotsManager)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats, asyncProvider, consensusRulesContainer)
        {
            this.FederationManager = federationManager;
            this.PoaHeaderValidator = poaHeaderValidator;
            this.SlotsManager = slotsManager;
        }

        /// <summary>
        /// This gets overridden in a tokenless network as we can't return coinview's tip as it does not apply.
        /// </summary>
        /// <returns>The <see cref="ChainIndexer"/>'s tip.</returns>
        public override uint256 GetBlockHash()
        {
            return base.ChainIndexer.Tip.HashBlock;
        }
    }
}
