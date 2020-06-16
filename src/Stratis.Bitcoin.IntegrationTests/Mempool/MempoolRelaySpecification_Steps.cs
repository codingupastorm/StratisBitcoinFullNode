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
using Org.BouncyCastle.X509;

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
            this.nodeA = this.nodeBuilder.CreateTokenlessNode(this.network, 0, this.server, permissions: TokenlessTestHelper.FederationPermissions);
            this.nodeB = this.nodeBuilder.CreateTokenlessNode(this.network, 1, this.server, permissions: TokenlessTestHelper.FederationPermissions);
            this.nodeC = this.nodeBuilder.CreateTokenlessNode(this.network, 2, this.server, permissions: TokenlessTestHelper.FederationPermissions);

            X509Certificate[] certificates = { this.nodeA.ClientCertificate.ToCertificate(), this.nodeB.ClientCertificate.ToCertificate(), this.nodeC.ClientCertificate.ToCertificate() };

            // Add certificates to nodeA.
            TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, this.nodeA.DataFolder, this.network);
            this.nodeA.Start();

            // Add certificates to nodeB.
            TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, this.nodeB.DataFolder , this.network);
            this.nodeB.Start();

            // Add certificates to nodeC.
            TokenlessTestHelper.AddCertificatesToMembershipServices(certificates, this.nodeC.DataFolder, this.network);
            this.nodeC.Start();
        }

        protected void nodeA_mines_blocks()
        {
            // nodeA mines a block.
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