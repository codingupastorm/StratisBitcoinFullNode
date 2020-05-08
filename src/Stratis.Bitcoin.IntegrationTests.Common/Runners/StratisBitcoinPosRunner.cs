﻿using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Core.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Api;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P;
using Stratis.Features.BlockStore;
using Stratis.Features.Consensus;
using Stratis.Features.MemoryPool;
using Stratis.Features.Miner;
using Stratis.Features.SQLiteWalletRepository;
using Stratis.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.Runners
{
    public sealed class StratisBitcoinPosRunner : NodeRunner
    {
        private readonly bool isGateway;

        public StratisBitcoinPosRunner(string dataDir, Network network, string agent = "StratisBitcoin", bool isGateway = false)
            : base(dataDir, agent)
        {
            this.Network = network;
            this.isGateway = isGateway;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, ProtocolVersion.PROVEN_HEADER_VERSION, this.Agent, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder });

            if (this.isGateway)
                settings.MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION;

            var builder = new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePosConsensus()
                .UseMempool()
                .UseWallet()
                .AddSQLiteWalletRepository()
                .AddPowPosMining()
                .UseApi()
                .UseTestChainedHeaderTree()
                .MockIBD();

            if (this.OverrideDateTimeProvider)
                builder.OverrideDateTimeProviderFor<MiningFeature>();

            if (!this.EnablePeerDiscovery)
            {
                builder.RemoveImplementation<PeerConnectorDiscovery>();
                builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());
            }

            this.FullNode = (FullNode)builder.Build();
        }

        /// <summary>
        /// Builds a node with POS miner and RPC enabled.
        /// </summary>
        /// <param name="dataDir">Data directory that the node should use.</param>
        /// <param name="staking">Flag to signal that the node should the start staking on start up or not.</param>
        /// <returns>Interface to the newly built node.</returns>
        /// <remarks>Currently the node built here does not actually stake as it has no coins in the wallet,
        /// but all the features required for it are enabled.</remarks>
        public static IFullNode BuildStakingNode(string dataDir, bool staking = true)
        {
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, args: new string[] { $"-datadir={dataDir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" });
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                                .UseBlockStore()
                                .UsePosConsensus()
                                .UseMempool()
                                .UseWallet()
                                .AddSQLiteWalletRepository()
                                .AddPowPosMining()
                                .MockIBD()
                                .UseTestChainedHeaderTree()
                                .Build();

            return fullNode;
        }
    }
}