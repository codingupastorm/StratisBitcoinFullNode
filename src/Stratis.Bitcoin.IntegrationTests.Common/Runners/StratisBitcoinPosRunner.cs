using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Core.Configuration;
using Stratis.Core.P2P;
using Stratis.Features.Api;
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
    }
}