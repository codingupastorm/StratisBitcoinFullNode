using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Core
{
    public interface ICoreComponent
    {
        IBlockStoreQueue BlockStoreQueue { get; }
        ChainIndexer ChainIndexer { get; }
        IChainState ChainState { get; }
        IConnectionManager ConnectionManager { get; }
        IConsensusManager ConsensusManager { get; }
        IInitialBlockDownloadState InitialBlockDownloadState { get; }
        ILoggerFactory LoggerFactory { get; }
        Network Network { get; }
        IPeerBanning PeerBanning { get; }
    }

    public sealed class CoreComponent : ICoreComponent
    {
        public IBlockStoreQueue BlockStoreQueue { get; }

        public ChainIndexer ChainIndexer { get; }

        public IChainState ChainState { get; }

        public IConnectionManager ConnectionManager { get; }

        public IConsensusManager ConsensusManager { get; }

        public IInitialBlockDownloadState InitialBlockDownloadState { get; }

        public ILoggerFactory LoggerFactory { get; }

        public Network Network { get; }

        public IPeerBanning PeerBanning { get; }

        public CoreComponent(
            IBlockStoreQueue blockStoreQueue,
            ChainIndexer chainIndexer,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            IInitialBlockDownloadState initialBlockDownloadState,
            ILoggerFactory loggerFactory,
            Network network,
            IPeerBanning peerBanning)
        {
            this.BlockStoreQueue = blockStoreQueue;
            this.ChainIndexer = chainIndexer;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
            this.ConsensusManager = consensusManager;
            this.InitialBlockDownloadState = initialBlockDownloadState;
            this.LoggerFactory = loggerFactory;
            this.Network = network;
            this.PeerBanning = peerBanning;
        }
    }
}
