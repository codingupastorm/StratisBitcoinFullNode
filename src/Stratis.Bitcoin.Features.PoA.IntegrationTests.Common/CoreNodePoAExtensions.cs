using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Features.PoA.IntegrationTests.Common
{
    public static class CoreNodePoAExtensions
    {
        public static async Task MineBlocksAsync(this CoreNode node, int count)
        {
            IPoAMiner miner = node.FullNode.NodeService<IPoAMiner>();

            if (miner is TestPoAMiner poaMiner)
            {
                await poaMiner.MineBlocksAsync(count);
            }
            else
            {
                await (miner as TokenlessTestPoAMiner).MineBlocksAsync(count);
            }
        }

        public static void WaitTillSynced(params CoreNode[] nodes)
        {
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(nodes[i], nodes[i + 1]));
            }
        }
    }
}
