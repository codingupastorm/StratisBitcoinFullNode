using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core;
using Stratis.Core.Base;
using Stratis.Core.Builder;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
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
    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(string dataDir, Network network, string agent)
            : base(dataDir, agent)
        {
            this.Network = network;
        }

        public override void BuildNode()
        {
            NodeSettings settings;

            if (string.IsNullOrEmpty(this.Agent))
                settings = new NodeSettings(this.Network, args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });
            else
                settings = new NodeSettings(this.Network, agent: this.Agent, args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder });

            var builder = new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UseBlockStore()
                            .UsePowConsensus()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddSQLiteWalletRepository()
                            .UseApi()
                            .UseTestChainedHeaderTree()
                            .MockIBD();

            if (this.ServiceToOverride != null)
                builder.OverrideService<BaseFeature>(this.ServiceToOverride);

            if (!this.EnablePeerDiscovery)
            {
                builder.RemoveImplementation<PeerConnectorDiscovery>();
                builder.ReplaceService<IPeerDiscovery, BaseFeature>(new PeerDiscoveryDisabled());
            }

            if (this.AlwaysFlushBlocks)
            {
                builder.ReplaceService<IBlockStoreQueueFlushCondition, BlockStoreFeature>(new BlockStoreAlwaysFlushCondition());
            }

            this.FullNode = (FullNode)builder.Build();
        }
    }
}