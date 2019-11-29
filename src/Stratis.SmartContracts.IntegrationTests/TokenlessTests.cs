using System;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
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
            this.network = new SmartContractsPoAWhitelistRegTest();

            this.builder = SmartContractNodeBuilder.Create(this);
            this.nodeFactory = (nodeIndex) => this.builder.CreateTokenlessSmartContractPoANode(this.network, nodeIndex).Start();
        }

        [Fact]
        public void CanDeployTokenlessContract()
        {
            using (var chain = new PoAMockChain(2, this.nodeFactory).Build())
            {
                MockChainNode node1 = chain.Nodes[0];
                MockChainNode node2 = chain.Nodes[1];
                this.SetupNodes(chain, node1, node2);

                // Compile file
                byte[] toSend = ContractCompiler.CompileFile("SmartContracts/StorageDemo.cs").Compilation;

                // Add the hash to all the nodes on the chain.
                chain.WhitelistCode(toSend);

                // Send create with value, and ensure balance is stored.
                BuildCreateContractTransactionResponse sendResponse = node1.SendCreateContractTransaction(toSend, 30);
                node1.WaitMempoolCount(1);
                chain.MineBlocks(1);

                // Check the balance exists at contract location.
                Assert.Equal((ulong)30 * 100_000_000, node1.GetContractBalance(sendResponse.NewContractAddress));
            }
        }

        public void Dispose()
        {
            this.builder.Dispose();
        }
    }
}
