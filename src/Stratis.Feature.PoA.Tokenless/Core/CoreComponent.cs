using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Base;
using Stratis.Core.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Core
{
    public interface ICoreComponent
    {
        IBlockStore BlockStore { get; }
        IBlockStoreQueue BlockStoreQueue { get; }
        ChainIndexer ChainIndexer { get; }
        IChainState ChainState { get; }
        IConnectionManager ConnectionManager { get; }
        IConsensusManager ConsensusManager { get; }
        IDateTimeProvider DateTimeProvider { get; }
        IInitialBlockDownloadState InitialBlockDownloadState { get; }
        ILoggerFactory LoggerFactory { get; }
        Network Network { get; }
        INodeLifetime NodeLifetime { get; }
        IPeerBanning PeerBanning { get; }
    }

    public sealed class CoreComponent : ICoreComponent
    {
        public IBlockStore BlockStore { get; }

        public IBlockStoreQueue BlockStoreQueue { get; }

        public ChainIndexer ChainIndexer { get; }

        public IChainState ChainState { get; }

        public IConnectionManager ConnectionManager { get; }

        public IConsensusManager ConsensusManager { get; }

        public IDateTimeProvider DateTimeProvider { get; }

        public IInitialBlockDownloadState InitialBlockDownloadState { get; }

        public ILoggerFactory LoggerFactory { get; }

        public Network Network { get; }

        public INodeLifetime NodeLifetime { get; }

        public IPeerBanning PeerBanning { get; }

        public CoreComponent(
            IBlockStore blockStore,
            IBlockStoreQueue blockStoreQueue,
            ChainIndexer chainIndexer,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            IInitialBlockDownloadState initialBlockDownloadState,
            ILoggerFactory loggerFactory,
            Network network,
            INodeLifetime nodeLifetime,
            IPeerBanning peerBanning)
        {
            this.BlockStore = blockStore;
            this.BlockStoreQueue = blockStoreQueue;
            this.ChainIndexer = chainIndexer;
            this.ChainState = chainState;
            this.ConnectionManager = connectionManager;
            this.ConsensusManager = consensusManager;
            this.DateTimeProvider = dateTimeProvider;
            this.InitialBlockDownloadState = initialBlockDownloadState;
            this.LoggerFactory = loggerFactory;
            this.Network = network;
            this.NodeLifetime = nodeLifetime;
            this.PeerBanning = peerBanning;
        }
    }
}
