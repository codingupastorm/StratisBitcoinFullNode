using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
        public async Task TokenlessNodeStarts_Async()
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
