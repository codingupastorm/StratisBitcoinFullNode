﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Features.SmartContracts.Rules;

namespace Stratis.Features.SmartContracts.MempoolRules
{
    /// <summary>
    /// Enforces that only certain script types are used on the network.
    /// </summary>
    /// <remarks>Shares logic with the consensus rule <see cref="AllowedScriptTypeRule"/></remarks>
    public class AllowedScriptTypeMempoolRule : MempoolRule
    {
        public AllowedScriptTypeMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            AllowedScriptTypeRule.CheckTransaction(this.network, context.Transaction);
        }
    }
}