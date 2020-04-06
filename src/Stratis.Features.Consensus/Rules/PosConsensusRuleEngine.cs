using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Features.Consensus.CoinViews;
using Stratis.Features.Consensus.Interfaces;
using Stratis.Features.Consensus.ProvenBlockHeaders;
using Stratis.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Features.Consensus.Rules
{
    /// <summary>
    /// Extension of consensus rules that provide access to a PoS store.
    /// </summary>
    /// <remarks>
    /// A Proof-Of-Stake blockchain as implemented in this code base represents a hybrid POS/POW consensus model.
    /// </remarks>
    public class PosConsensusRuleEngine : PowConsensusRuleEngine
    {
        /// <summary>Database of stake related data for the current blockchain.</summary>
        public IStakeChain StakeChain { get; }

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        public IStakeValidator StakeValidator { get; }

        public IRewindDataIndexCache RewindDataIndexCache { get; }

        public PosConsensusRuleEngine(Network network, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, ChainIndexer chainIndexer, NodeDeployments nodeDeployments,
            ConsensusSettings consensusSettings, ICheckpoints checkpoints, ICoinView utxoSet, IStakeChain stakeChain, IStakeValidator stakeValidator, IChainState chainState,
            IInvalidBlockHashStore invalidBlockHashStore, INodeStats nodeStats, IRewindDataIndexCache rewindDataIndexCache, IAsyncProvider asyncProvider, ConsensusRulesContainer consensusRulesContainer)
            : base(network, loggerFactory, dateTimeProvider, chainIndexer, nodeDeployments, consensusSettings, checkpoints, utxoSet, chainState, invalidBlockHashStore, nodeStats, asyncProvider, consensusRulesContainer)
        {
            this.StakeChain = stakeChain;
            this.StakeValidator = stakeValidator;
            this.RewindDataIndexCache = rewindDataIndexCache;
        }

        /// <inheritdoc />
        [NoTrace]
        public override RuleContext CreateRuleContext(ValidationContext validationContext)
        {
            return new PosRuleContext(validationContext, this.DateTimeProvider.GetTimeOffset());
        }

        /// <inheritdoc />
        public override void Initialize(ChainedHeader chainTip)
        {
            base.Initialize(chainTip);

            this.StakeChain.Load();

            // A temporary hack until tip manage will be introduced.
            var breezeCoinView = (DBCoinView)((CachedCoinView)this.UtxoSet).Inner;
            uint256 hash = breezeCoinView.GetTipHash();
            ChainedHeader tip = chainTip.FindAncestorOrSelf(hash);

            this.RewindDataIndexCache.Initialize(tip.Height, this.UtxoSet);
        }

        /// <inheritdoc />
        public override void ConsensusSpecificRequiredTxChecks(Transaction tx)
        {
            long adjustedTime = this.DateTimeProvider.GetAdjustedTimeAsUnixTimestamp();
            PosFutureDriftRule futureDriftRule = this.GetRule<PosFutureDriftRule>();

            // nTime has different purpose from nLockTime but can be used in similar attacks
            if (tx.Time > adjustedTime + futureDriftRule.GetFutureDrift(adjustedTime))
                ConsensusErrors.TimeTooNew.Throw();
        }

        /// <inheritdoc />
        public override void ConsensusSpecificTxChecks(Transaction tx)
        {
            base.ConsensusSpecificTxChecks(tx);

            new CheckPosTransactionRule { Logger = this.logger }.CheckTransaction(tx);
        }
    }
}