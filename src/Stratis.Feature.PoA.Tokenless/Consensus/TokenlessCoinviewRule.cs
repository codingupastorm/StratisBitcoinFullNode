using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Core.Rules
{
    public sealed class TokenlessCoinviewRule : CoinViewRule
    {
        private TokenlessCoinViewRuleLogic logic;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly IBlockExecutionResultCache executionCache;
        private readonly ILoggerFactory loggerFactory;

        public TokenlessCoinviewRule(
            IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView,
            IBlockExecutionResultCache executionCache,
            ILoggerFactory loggerFactory)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
            this.executionCache = executionCache;
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            this.logic = new TokenlessCoinViewRuleLogic(this.callDataSerializer, this.coinView, this.executionCache, this.executorFactory, this.loggerFactory, this.receiptRepository, this.senderRetriever, this.stateRepositoryRoot);
        }

        /// <inheritdoc />
        public override async Task RunAsync(RuleContext context)
        {
            await this.logic.RunAsync(context);
        }

        /// <inheritdoc/>
        protected override bool CheckInput(Transaction tx, int inputIndexCopy, TxOut txout, PrecomputedTransactionData txData, TxIn input, DeploymentFlags flags)
        {
            return this.logic.CheckInput(tx, inputIndexCopy, txout, txData, input, flags);
        }

        /// <summary>
        /// Executes contracts as necessary and updates the coinview / UTXOset after execution.
        /// </summary>
        /// <inheritdoc/>
        public override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        protected override Money GetTransactionFee(UnspentOutputSet view, Transaction tx)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override void CheckBlockReward(RuleContext context, Money fees, int height, Block block)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override void CheckMaturity(UnspentOutputs coins, int spendHeight)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }

        public override Money GetProofOfWorkReward(int height)
        {
            throw new InvalidOperationException("Not valid in a tokenless blockchain.");
        }
    }
}
