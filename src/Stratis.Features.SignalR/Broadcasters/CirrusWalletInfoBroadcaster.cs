using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Core.AsyncWork;
using Stratis.Core.Utilities;
using Stratis.Features.Wallet.Interfaces;

namespace Stratis.Features.SignalR.Broadcasters
{
    /// <summary>
    /// Broadcasts current staking information to SignalR clients
    /// </summary>
    public class CirrusWalletInfoBroadcaster : WalletInfoBroadcaster
    {
        public CirrusWalletInfoBroadcaster(
            ILoggerFactory loggerFactory,
            IWalletManager walletManager,
            IConsensusManager consensusManager,
            IConnectionManager connectionManager,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            ChainIndexer chainIndexer,
            EventsHub eventsHub)
            : base(loggerFactory, walletManager, consensusManager, connectionManager, asyncProvider, nodeLifetime,
                chainIndexer, eventsHub, true)
        {
        }
    }
}