using System.Collections.Generic;
using System.Threading;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Tests.Common.MockChain;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class ApiTest : IClassFixture<PoAMockChainFixture>
    {
        private readonly IMockChain mockChain;

        public ApiTest(PoAMockChainFixture fixture)
        {
            this.mockChain = fixture.Chain;
        }

        [Fact]
        public void TestApi()
        {
            var node1 = this.mockChain.Nodes[0];
            var node2 = this.mockChain.Nodes[1];

            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/StandardToken.cs");
            Assert.True(compilationResult.Success);
            BuildCreateContractTransactionResponse response = node1.SendCreateContractTransaction(compilationResult.Compilation, 0, gasLimit: SmartContractFormatLogic.GasLimitMaximum,
                parameters: new string[]
                {
                    "7#50000000000000000"
                });

            this.mockChain.WaitAllMempoolCount(1);
            this.mockChain.MineBlocks(1);

            var receipt = node1.GetReceipt(response.TransactionId.ToString());
            Assert.True(receipt.Success);

            string contractAddress = receipt.NewContractAddress;
            string sender = node1.MinerAddress.Address;
            int apiPort = node1.CoreNode.ApiPort;

            while (true)
            {
                Thread.Sleep(10_000);
                this.mockChain.MineBlocks(1);
                IList<ReceiptResponse> receipts = node1.GetReceipts(contractAddress, "TransferLog");
            }
        }
    }
}
