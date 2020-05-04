using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Features.SmartContracts.MempoolRules
{
    /// <summary>
    /// Used to check that people don't try and send funds to contracts via P2PKH.
    /// </summary>
    public class P2PKHNotContractMempoolRule : MempoolRule
    {
        private readonly IStateRepositoryRoot stateRepositoryRoot;

        public P2PKHNotContractMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IStateRepositoryRoot stateRepositoryRoot,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.stateRepositoryRoot = stateRepositoryRoot;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            P2PKHNotContractRule.CheckTransaction(this.stateRepositoryRoot, context.Transaction);
        }
    }
}