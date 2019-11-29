using System;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class TokenlessTests : IDisposable
    {
        private readonly SmartContractsPoARegTest network;
        private readonly Func<int, CoreNode> nodeFactory;
        private readonly SmartContractNodeBuilder builder;

        public TokenlessTests()
        {
            this.network = new SmartContractsPoARegTest();

            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodeFactory = (nodeIndex) => this.builder.CreateTokenlessSmartContractPoANode(this.network, nodeIndex).Start();
        }

        [Fact]
        public void CanDeployTokenlessContract()
        {
            // TODO: Continue from here. 

            // Up to the point where the contract is being retrieved.

            using (var chain = new PoAMockChain(2, this.nodeFactory).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Compile file
                byte[] toSend = ContractCompiler.CompileFile("SmartContracts/TokenlessExample.cs").Compilation;

                Assert.NotNull(toSend);

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 0);
                node1.WaitMempoolCount(1);
                chain.MineBlocks(1);

                // Check the balance exists at contract location.
                Assert.NotNull(node1.GetCode(sendResponse.NewContractAddress));
            }
        }

        private void SetupNodes(IMockChain chain, MockChainNode node1, MockChainNode node2)
        {
            // TODO: Use ready chain data
            // Get premine
            chain.MineBlocks(10);

            // Send half to other from whoever received premine
            if ((long)node1.WalletSpendableBalance == node1.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi)
            {
                PayHalfPremine(chain, node1, node2);
            }
            else
            {
                PayHalfPremine(chain, node2, node1);
            }
        }

        private void PayHalfPremine(IMockChain chain, MockChainNode from, MockChainNode to)
        {
            from.SendTransaction(to.MinerAddress.ScriptPubKey, new Money(from.CoreNode.FullNode.Network.Consensus.PremineReward.Satoshi / 2, MoneyUnit.Satoshi));
            from.WaitMempoolCount(1);
            chain.MineBlocks(1);
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}
