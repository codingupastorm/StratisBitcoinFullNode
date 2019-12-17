using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless
{
    public class TokenlessPoAMiner : PoAMiner
    {
        public TokenlessPoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            ISlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            IFederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            PoAMinerSettings poAMinerSettings,
            IAsyncProvider asyncProvider) 
            : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager, connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, null, poAMinerSettings, asyncProvider)
        {
        }

        protected override Script GetScriptPubKeyFromWallet()
        {
            // Will ensure that an empty script is used to build blocks.
            return null;
        }
    }
}
