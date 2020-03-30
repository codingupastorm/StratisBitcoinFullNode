using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public sealed class TokenlessNodeChannelNetworkTests
    {
        /// <summary>
        /// Proves that TokenlessD can start with a serialized version of the network.
        /// </summary>
        [Fact]
        public void CanStartSystemChannelNode()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(testRootFolder).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var tokenlessNetwork = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, tokenlessNetwork));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);
                CaClient client1 = TokenlessTestHelper.GetClient(server);

                // Create the main tokenless node.
                CoreNode tokenlessNode = nodeBuilder.CreateTokenlessNode(tokenlessNetwork, 0, ac, client1);
                tokenlessNode.Start();

                // Create and start the channel node.
                CoreNode channelNode = nodeBuilder.CreateChannelNode(tokenlessNode, "system");
                channelNode.Start();
            }
        }

        [Fact]
        public void TokenlessNodeCanCreateAndStartChannelNodes()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(TokenlessTestHelper.GetDataFolderName()).Build())
            using (SmartContractNodeBuilder nodeBuilder = SmartContractNodeBuilder.Create(testRootFolder))
            {
                var network = new TokenlessNetwork();

                server.Start();

                // Start + Initialize CA.
                var client = TokenlessTestHelper.GetAdminClient();
                Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, network));

                // Get Authority Certificate.
                X509Certificate ac = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);
                CaClient client1 = TokenlessTestHelper.GetClient(server);

                // Create and start the main "infra" tokenless node which will internal start the "sytem channel node".
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, ac, client1);
                infraNode.Start();

                // Ask the infra node to create the "Sales" channel.
                // This effectively serializes the channel network and returns the path where the json is located at.
                //var networkFolderPath = tokenlessNode.CreateChannel("Sales");
                //var channelNode = nodeBuilder.CreateInfrastructureNode(networkFolderPath);
                //channelNode.Start();
            }
        }
    }
}
