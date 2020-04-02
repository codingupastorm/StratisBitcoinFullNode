using System.Diagnostics;
using System.Linq;
using CertificateAuthority;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Channels;
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
        public void InfraNodeCanCreateAndStartSystemChannelNode()
        {
            TokenlessTestHelper.GetTestRootFolder(out string testRootFolder);

            Process channelNodeProcess = null;

            using (IWebHost server = TokenlessTestHelper.CreateWebHostBuilder(testRootFolder).Build())
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

                // Create and start the main "infra" tokenless node which will internally start the "system channel node".
                CoreNode infraNode = nodeBuilder.CreateInfraNode(network, 0, ac, client1);
                infraNode.Start();

                var channelService = infraNode.FullNode.NodeService<IChannelService>();
                Assert.True(channelService.StartedChannelNodes.Count == 1);

                channelNodeProcess = Process.GetProcessById(channelService.StartedChannelNodes.First());
                Assert.False(channelNodeProcess.HasExited);
            }

            Assert.True(channelNodeProcess.HasExited);
        }
    }
}
