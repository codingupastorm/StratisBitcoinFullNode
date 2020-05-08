using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Core.Utilities;
using Stratis.Features.Api;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.PoA;
using Stratis.Features.SQLiteWalletRepository;
using Stratis.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.PoA
{
    public class PoANodeRunner : NodeRunner
    {
        private readonly IDateTimeProvider timeProvider;

        public PoANodeRunner(string dataDir, PoANetwork network, EditableTimeProvider timeProvider)
            : base(dataDir, null)
        {
            this.Network = network;
            this.timeProvider = timeProvider;
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(this.Network, args: new string[] { "-conf=poa.conf", "-datadir=" + this.DataFolder });

            this.FullNode = (FullNode)new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UsePoAConsensus(this.Network)
                .UseMempool()
                .UseWallet()
                .AddSQLiteWalletRepository()
                .UseApi()
                .MockIBD()
                .UseTestChainedHeaderTree()
                .ReplaceTimeProvider(this.timeProvider)
                .AddFastMiningCapability()
                .Build();
        }
    }
}
