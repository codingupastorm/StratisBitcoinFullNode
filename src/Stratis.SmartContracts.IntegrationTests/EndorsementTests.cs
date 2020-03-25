using System;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Controllers;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class EndorsementTests
    {
        // This comes from CaTestHelper. 
        private const string OrganisationName = "dummyOrganization";

        private TokenlessNetwork network;

        public EndorsementTests()
        {
            this.network = TokenlessTestHelper.Network;
        }

        [Fact]
        public async Task EndorseCallTransaction()
        {
            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(this))
            {
                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                CaClient client1 = TokenlessTestHelper.GetClient(server);
                CaClient client2 = TokenlessTestHelper.GetClient(server);

                CoreNode node1 = nodeBuilder.CreateFullTokenlessNode(this.network, 0, ac, client1);
                CoreNode node2 = nodeBuilder.CreateFullTokenlessNode(this.network, 1, ac, client2);

                node1.Start();
                node2.Start();

                TestHelper.Connect(node1, node2);

                // Broadcast from node1, check state of node2.
                var receiptRepository = node2.FullNode.NodeService<IReceiptRepository>();
                var stateRepo = node2.FullNode.NodeService<IStateRepositoryRoot>();

                Transaction createTransaction = TokenlessTestHelper.CreateContractCreateTransaction(node1, node1.TransactionSigningPrivateKey);
                await node1.BroadcastTransactionAsync(createTransaction);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().GetMempoolAsync().Result.Count > 0);
                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Receipt createReceipt = receiptRepository.Retrieve(createTransaction.GetHash());
                Assert.True(createReceipt.Success);

                Transaction callTransaction = TokenlessTestHelper.CreateContractCallTransaction(node1, createReceipt.NewContractAddress, node1.TransactionSigningPrivateKey);

                var tokenlessController = node1.FullNode.NodeController<TokenlessController>();
                JsonResult result = (JsonResult) await tokenlessController.SendProposalAsync(new SendProposalModel
                {
                    TransactionHex = callTransaction.ToHex(),
                    Organisation = OrganisationName
                });

                var endorsementResponse = (SendProposalResponseModel) result.Value;
                Assert.Equal("Transaction has been sent to endorsing node for execution.", endorsementResponse.Message);

                TestBase.WaitLoop(() => node1.FullNode.MempoolManager().InfoAll().Count > 0);
                TestBase.WaitLoop(() => node2.FullNode.MempoolManager().InfoAll().Count > 0);

                await node1.MineBlocksAsync(1);
                TokenlessTestHelper.WaitForNodeToSync(node1, node2);

                Assert.Equal(BitConverter.GetBytes(101), stateRepo.GetStorageValue(createReceipt.NewContractAddress, Encoding.UTF8.GetBytes("Increment")).Value);
            }
        }
    }
}
