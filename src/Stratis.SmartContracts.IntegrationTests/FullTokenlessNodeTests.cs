using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
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
        public async Task TokenlessNodesConnectAndMineOpReturnAsync()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateFullTokenlessNode(this.network, 0);
                CoreNode node2 = builder.CreateFullTokenlessNode(this.network, 1);

                node1.Start();
                node2.Start();

                Assert.True(node1.FullNode.NodeService<StoreSettings>().TxIndex);
                Assert.True(node2.FullNode.NodeService<StoreSettings>().TxIndex);

                TestHelper.Connect(node1, node2);

                TestBase.WaitLoop(() => node1.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);
                TestBase.WaitLoop(() => node2.FullNode.ConnectionManager.ConnectedPeers.Count() == 1);

                Transaction transaction = this.CreateBasicOpReturnTransaction(node1);

                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                await broadcasterManager.BroadcastTransactionAsync(transaction);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);

                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 1);
            }
        }

        [Fact]
        public async Task TokenlessNodesCreateAndCallAContract()
        {
            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateFullTokenlessNode(this.network, 0);
                CoreNode node2 = builder.CreateFullTokenlessNode(this.network, 1);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var broadcasterManager = node1.FullNode.NodeService<IBroadcasterManager>();
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = this.CreateContractCreateTransaction(node1);
                await broadcasterManager.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 1);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = CreateContractCallTransaction(node1, createReceipt.NewContractAddress);
                await broadcasterManager.BroadcastTransactionAsync(callTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TestBase.WaitLoop(() => node2.FullNode.ChainIndexer.Height == 2);

                Receipt callReceipt = receiptRepository.Retrieve(callTransaction.GetHash());
                Assert.True(callReceipt.Success);
                
                Assert.NotNull(stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")));
            }
        }

        private Transaction CreateContractCreateTransaction(CoreNode node)
        {
            Transaction transaction = this.network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateContractCallTransaction(CoreNode node, uint160 address)
        {
            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0,0,(Gas)0, address, "CallMe");
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }

        private Transaction CreateBasicOpReturnTransaction(CoreNode node)
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            var key = new Key();

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }
    }
}
