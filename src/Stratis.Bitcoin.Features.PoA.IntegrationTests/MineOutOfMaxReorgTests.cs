using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class MineOutOfMaxReorgTests
    {
        private readonly TestPoANetwork network;

        private readonly PoANodeBuilder builder;

        private readonly CoreNode node1, node2;

        public MineOutOfMaxReorgTests()
        {
            // This network has MaxReorgLength set to 5.
            this.network = new TestPoANetwork();

            this.builder = PoANodeBuilder.CreatePoANodeBuilder(this);

            this.node1 = this.builder.CreatePoANode(this.network, this.network.FederationKey1).Start();
            this.node2 = this.builder.CreatePoANode(this.network, this.network.FederationKey2).Start();
        }

        [Fact]
        public async Task MinerGetsBannedMiningOutOfMaxReorg()
        {
            // Put one node very far ahead with fast poa mining.
            await this.node1.MineBlocksAsync(1000);

            TestHelper.Connect(this.node1, this.node2);

            // Virtually instantly mine a block.
            await this.node2.MineBlocksAsync(1);

            // First node gets rid of a peer. Bans him for pushing blocks on a chain out of MaxReorg
            TestBase.WaitLoop(() => !this.node1.FullNode.ConnectionManager.ConnectedPeers.Any());
        }
    }
}
