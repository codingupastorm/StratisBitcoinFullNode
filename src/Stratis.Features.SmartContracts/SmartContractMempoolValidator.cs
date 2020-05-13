using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Base.Deployments;
using Stratis.Core.Configuration;
using Stratis.Core.Consensus;
using Stratis.Core.Utilities;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;

namespace Stratis.Features.SmartContracts
{
    /// <summary>
    /// Provides the same functionality as the original mempool validator with some extra validation.
    /// </summary>
    public class SmartContractMempoolValidator : MempoolValidator
    {
        /// <summary>The "functional" minimum gas limit. Not enforced by consensus but miners are only going to pick transactions up if their gas price is higher than this.</summary>
        public const ulong MinGasPrice = 100;

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock,
            IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ChainIndexer chainIndexer,
            ICoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings,
            IConsensusRuleEngine consensusRules, IEnumerable<IMempoolRule> mempoolRules, NodeDeployments nodeDeployments)
            : base(memPool, mempoolLock, dateTimeProvider, mempoolSettings, chainIndexer, coinView, loggerFactory, nodeSettings, consensusRules, mempoolRules, nodeDeployments)
        {
            // Dirty hack, but due to AllowedScriptTypeRule we don't need to check for standard scripts on any network, even live.
            // TODO: Remove ASAP. Ensure RequireStandard isn't used on SC mainnets, or the StandardScripts check is modular.
            mempoolSettings.RequireStandard = false;
        }
    }
}