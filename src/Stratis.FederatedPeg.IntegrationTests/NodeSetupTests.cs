using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.Features.FederationGateway.NetworkHelpers;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class NodeSetupTests
    {
        [Fact]
        public void NodeSetup()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartMainNodes();

                context.MainUser.State.Should().Be(CoreNodeState.Running);
                context.FedMain1.State.Should().Be(CoreNodeState.Running);
                context.FedMain2.State.Should().Be(CoreNodeState.Running);
                context.FedMain3.State.Should().Be(CoreNodeState.Running);

                context.StartSideNodes();

                context.SideUser.State.Should().Be(CoreNodeState.Running);
                context.FedSide1.State.Should().Be(CoreNodeState.Running);
                context.FedSide2.State.Should().Be(CoreNodeState.Running);
                context.FedSide3.State.Should().Be(CoreNodeState.Running);
            }
        }

        [Fact]
        public void EnableNodeWallets()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartAndConnectNodes();

                context.EnableFederationWallets(context.SideChainFedNodes);
                context.EnableFederationWallets(context.MainChainFedNodes);
            }
        }

        [Fact]
        public void FundMainChainNode()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartMainNodes();
                context.ConnectMainChainNodes();

                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + 1);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1, context.FedMain2, context.FedMain3);
                Assert.Equal(context.MainChainNetwork.Consensus.ProofOfWorkReward, context.GetBalance(context.MainUser));
            }
        }
        
        [Fact]
        public void Sidechain_Premine_Received()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                context.StartSideNodes();
                context.ConnectSideChainNodes();

                // Wait for node to reach premine height 
                TestHelper.WaitLoop(() => context.SideUser.FullNode.Chain.Height == context.SideUser.FullNode.Network.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1, context.FedSide2, context.FedSide3);

                // Ensure that coinbase contains premine reward and it goes to the fed.
                Block block = context.SideUser.FullNode.Chain.Tip.Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);
            }
        }

        [Fact]
        public void End_To_End()
        {
            using (SidechainTestContext context = new SidechainTestContext())
            {
                // Set everything up
                context.StartAndConnectNodes();
                context.EnableFederationWallets(context.SideChainFedNodes);
                context.EnableFederationWallets(context.MainChainFedNodes);

                // Fund a main chain node
                TestHelper.MineBlocks(context.MainUser, (int)context.MainChainNetwork.Consensus.CoinbaseMaturity + 10);
                TestHelper.WaitForNodeToSync(context.MainUser, context.FedMain1, context.FedMain2, context.FedMain3);
                Assert.Equal(10 * context.MainChainNetwork.Consensus.ProofOfWorkReward, context.GetBalance(context.MainUser));

                // Let sidechain progress to point where fed has the premine
                TestHelper.WaitLoop(() => context.SideUser.FullNode.Chain.Height == context.SideUser.FullNode.Network.Consensus.PremineHeight);
                TestHelper.WaitForNodeToSync(context.SideUser, context.FedSide1, context.FedSide2, context.FedSide3);
                Block block = context.SideUser.FullNode.Chain.GetBlock((int) context.SideChainNetwork.Consensus.PremineHeight).Block;
                Transaction coinbase = block.Transactions[0];
                Assert.Single(coinbase.Outputs);
                Assert.Equal(context.SideChainNetwork.Consensus.PremineReward, coinbase.Outputs[0].Value);
                Assert.Equal(context.scriptAndAddresses.payToMultiSig.PaymentScript, coinbase.Outputs[0].ScriptPubKey);
            }
        }
    }
}
