using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.SmartContracts.IntegrationTests.MockChain;
using Stratis.SmartContracts.IntegrationTests.PoA.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests.PoA
{
    public class ChainBuildsTest
    {

        [Fact]
        public void PoAMockChain_Node_Builds_And_Mines()
        {
            using (PoAMockChain chain = new PoAMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];

                int tipBefore = node1.CoreNode.GetTip().Height;
                
                chain.MineBlocks(1);

                Assert.True(node1.CoreNode.GetTip().Height == tipBefore + 1);
                Assert.True(node2.CoreNode.GetTip().Height == tipBefore + 1);

                tipBefore = node1.CoreNode.GetTip().Height;
                chain.MineBlocks(2);

                Assert.True(node1.CoreNode.GetTip().Height == tipBefore + 2);
                Assert.True(node2.CoreNode.GetTip().Height == tipBefore + 2);
            }
        }

        [Fact]
        public void PoAMockChain_Node_HasBalance_FromPremine()
        {
            using (PoAMockChain chain = new PoAMockChain(2))
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                // TODO: Get code from PoA MiningTests
                TestHelper.WaitLoop(() => node1.CoreNode.GetTip().Height >= chain.Network.Consensus.PremineHeight + chain.Network.Consensus.CoinbaseMaturity + 1);
                Money totalValue = node1.WalletSpendableBalance + node2.WalletSpendableBalance;
            }
        }

    }
}
