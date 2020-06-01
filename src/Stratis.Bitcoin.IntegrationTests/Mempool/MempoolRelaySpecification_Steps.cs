using System.Linq;
using FluentAssertions;
using FluentAssertions.Common;
using NBitcoin;
using Stratis.Core.Connection;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Stratis.Feature.PoA.Tokenless.Networks;
using CertificateAuthority.Tests.Common;
using Stratis.SmartContracts.Tests.Common;
using Xunit;
using System.Collections.Generic;
using CertificateAuthority;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public partial class MempoolRelaySpecification
    {
        private IWebHost server;
        private SmartContractNodeBuilder nodeBuilder;
        private CoreNode nodeA;
        private CoreNode nodeB;
        private CoreNode nodeC;
        private Transaction transaction;
        private TokenlessNetwork network;

        // NOTE: This constructor allows test step names to be logged
        public MempoolRelaySpecification(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override void BeforeTest()
        {
            this.network = new TokenlessNetwork();

            TestBase.GetTestRootFolder(out string testRootFolder);

            this.server = CaTestHelper.CreateWebHostBuilder(testRootFolder).Build();
            this.nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder);

            this.server.Start();

            // Start + Initialize CA.
            var client = TokenlessTestHelper.GetAdminClient(this.server);
            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            this.nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder);
        }

        protected override void AfterTest()
        {
            if (this.server != null)
            {
                this.server.Dispose();
                this.server = null;
            }

            this.nodeBuilder.Dispose();
        }

        protected void nodeA_nodeB_and_nodeC()
        {
            this.nodeA = this.nodeBuilder.CreateTokenlessNode(this.network, 0, this.server, permissions: new List<string>() { CaCertificatesManager.SendPermission, CaCertificatesManager.MiningPermission }).Start();
            this.nodeB = this.nodeBuilder.CreateTokenlessNode(this.network, 1, this.server).Start();
            this.nodeC = this.nodeBuilder.CreateTokenlessNode(this.network, 2, this.server).Start();
        }

        protected void nodeA_mines_blocks()
        {
            // add some coins to nodeA
            this.nodeA.MineBlocksAsync(1).GetAwaiter().GetResult();
        }

        protected void nodeA_connects_to_nodeB()
        {
            TestHelper.ConnectAndSync(this.nodeA, this.nodeB);
        }

        protected void nodeA_nodeB_and_nodeC_are_NON_whitelisted()
        {
            this.nodeA.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
            this.nodeB.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
            this.nodeC.FullNode.NodeService<IConnectionManager>().ConnectedPeers.First().Behavior<ConnectionManagerBehavior>().Whitelisted = false;
        }

        protected void nodeB_connects_to_nodeC()
        {
            TestHelper.ConnectAndSync(this.nodeB, this.nodeC);
        }

        protected void nodeA_creates_a_transaction_and_propagates_to_nodeB()
        {
            this.transaction = TokenlessTestHelper.CreateBasicOpReturnTransaction(this.nodeA);
            this.nodeA.BroadcastTransactionAsync(this.transaction).GetAwaiter().GetResult();
        }

        protected void the_transaction_is_propagated_to_nodeC()
        {
            TestBase.WaitLoop(() => this.nodeC.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Any());

            this.nodeC.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult()
                .Should().ContainSingle()
                .Which.IsSameOrEqualTo(this.transaction.GetHash());
        }
    }
}