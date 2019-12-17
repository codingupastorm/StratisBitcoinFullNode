using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Core;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessPoAMiner : PoAMiner
    {
        public TokenlessPoAMiner(
            ICoreComponent coreComponent,
            BlockDefinition blockDefinition,
            ISlotsManager slotsManager,
            PoABlockHeaderValidator poaHeaderValidator,
            IFederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            PoAMinerSettings poAMinerSettings,
            IAsyncProvider asyncProvider)
            : base(coreComponent.ConsensusManager, coreComponent.DateTimeProvider, coreComponent.Network, coreComponent.NodeLifetime, coreComponent.LoggerFactory, coreComponent.InitialBlockDownloadState, blockDefinition, slotsManager, coreComponent.ConnectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, null, poAMinerSettings, asyncProvider)
        {
        }

        /// <summary>
        /// Overriding this ensures that an empty script is used to build blocks.
        /// </summary>
        /// <returns>Returns a <c>null</c> script.</returns>
        protected override Script GetScriptPubKeyFromWallet()
        {
            return null;
        }
    }
}
