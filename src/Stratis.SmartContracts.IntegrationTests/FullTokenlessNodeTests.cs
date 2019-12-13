using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
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
        public void TokenlessNodeStarts()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node = builder.CreateFullTokenlessNode(this.network, 0);

                node.Start();
            }
        }
    }
}
