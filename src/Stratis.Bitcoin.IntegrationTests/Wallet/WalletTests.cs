using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.AsyncWork.JsonErrors;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletTests
    {
        private readonly Network network;

        public WalletTests()
        {
            this.network = new BitcoinRegTest();
        }

        /// <summary>
        /// Given_TheNodeHadAReorg_And_ConsensusTipIsdifferentFromWalletTip_When_ANewBlockArrives_Then_WalletCanRecover
        /// </summary>
        [Fact]
        public void WalletTestsScenario4()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisReceiver = builder.CreateStratisPowNode(this.network).Start();
                CoreNode stratisReorg = builder.CreateStratisPowNode(this.network).WithDummyWallet().Start();

                // Sync all nodes.
                TestHelper.ConnectAndSync(stratisReceiver, stratisSender);
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Remove the reorg node and wait for node to be disconnected.
                TestHelper.Disconnect(stratisReceiver, stratisReorg);
                TestHelper.Disconnect(stratisSender, stratisReorg);

                // Create a reorg by mining on two different chains.
                // Advance both chains, one chain is longer.
                TestHelper.MineBlocks(stratisSender, 2);
                TestHelper.MineBlocks(stratisReorg, 10);

                // Connect the reorg chain.
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Wait for the chains to catch up.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(20, stratisReceiver.FullNode.ChainIndexer.Tip.Height);

                // Rewind the wallet in the stratisReceiver node.
                (stratisReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(10);

                TestHelper.MineBlocks(stratisSender, 5);

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(25, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
            }
        }

        [Fact]
        public void WalletCanCatchupWithBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisminer = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();

                // Push the wallet back.
                stratisminer.FullNode.NodeService<IWalletSyncManager>().SyncFromHeight(5);

                TestHelper.MineBlocks(stratisminer, 5);
            }
        }

        public static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }
    }
}