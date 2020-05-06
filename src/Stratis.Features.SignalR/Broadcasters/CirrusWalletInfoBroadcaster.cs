using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Features.Wallet.Interfaces;
using Stratis.Core.AsyncWork;
using Stratis.Bitcoin.Consensus;

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