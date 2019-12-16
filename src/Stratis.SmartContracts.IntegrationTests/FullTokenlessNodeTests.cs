using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class FullTokenlessNodeTests
    {
        private readonly TokenlessNetwork network;

        public FullTokenlessNodeTests()
        {
            this.network = new TokenlessNetwork();
        }

        [Fact]
        public async Task TokenlessNodeStarts()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateFullTokenlessNode(this.network, 0);
                CoreNode node2 = builder.CreateFullTokenlessNode(this.network, 1);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);
                
                TestBase.WaitLoop(() => node1.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);
                TestBase.WaitLoop(() => node2.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);
            }
        }
    }
}
