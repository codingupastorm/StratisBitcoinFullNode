using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.SmartContracts.IntegrationTests
{
    /// <summary>
    /// Provides test helper methods that would otherwise fail on tokenless nodes because of dependencies missing.
    /// Specifically it avoids comparing wallet details.
    /// </summary>
    public static class TokenlessTestHelper
    {
        public static void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(n => TestBase.WaitLoop(() => IsNodeSynced(n)));
            nodes.Skip(1).ToList().ForEach(n => TestBase.WaitLoop(() => AreNodesSynced(nodes.First(), n)));
        }

        private static bool IsNodeSynced(CoreNode node)
        {
            // If the node is at genesis it is considered synced.
            if (node.FullNode.ChainIndexer.Tip.Height == 0)
                return true;

            if (node.FullNode.ChainIndexer.Tip.HashBlock != node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            // Check that node1 tip exists in store (either in disk or in the pending list)
            if (node.FullNode.BlockStore().GetBlock(node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            return true;
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2, bool ignoreMempool = false)
        {
            // If the nodes are at genesis they are considered synced.
            if (node1.FullNode.ChainIndexer.Tip.Height == 0 && node2.FullNode.ChainIndexer.Tip.Height == 0)
                return true;

            if (node1.FullNode.ChainIndexer.Tip.HashBlock != node2.FullNode.ChainIndexer.Tip.HashBlock)
                return false;

            if (node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            // Check that node1 tip exists in node2 store (either in disk or in the pending list)
            if (node1.FullNode.BlockStore().GetBlock(node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            // Check that node2 tip exists in node1 store (either in disk or in the pending list)
            if (node2.FullNode.BlockStore().GetBlock(node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            if (!ignoreMempool)
            {
                if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count)
                    return false;
            }

            return true;
        }

        public static async Task BroadcastTransactionAsync(this CoreNode node, Transaction transaction)
        {
            var broadcasterManager = node.FullNode.NodeService<IBroadcasterManager>();
            await broadcasterManager.BroadcastTransactionAsync(transaction);
        }
    }
}
