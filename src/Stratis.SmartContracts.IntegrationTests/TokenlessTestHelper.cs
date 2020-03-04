using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.RuntimeObserver;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    /// <summary>
    /// Provides test helper methods that would otherwise fail on tokenless nodes because of dependencies missing.
    /// Specifically it avoids comparing wallet details.
    /// </summary>
    public static class TokenlessTestHelper
    {
        public static readonly TokenlessNetwork Network = new TokenlessNetwork();
        private static readonly string BaseAddress = "http://localhost:5050";

        public static void WaitForNodeToSync(params CoreNode[] nodes)
        {
            nodes.ToList().ForEach(n => TestBase.WaitLoop(() => IsNodeSynced(n)));
            nodes.Skip(1).ToList().ForEach(n => TestBase.WaitLoop(() => AreNodesSynced(nodes.First(), n)));
        }

        private static bool IsNodeSynced(CoreNode node)
        {
            // If the node is at genesis it is considered synced.
            if (node.FullNode.ChainIndexer.Tip.Height == 0)
                return true;

            if (node.FullNode.ChainIndexer.Tip.HashBlock != node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            // Check that node1 tip exists in store (either in disk or in the pending list)
            if (node.FullNode.BlockStore().GetBlock(node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            return true;
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2, bool ignoreMempool = false)
        {
            // If the nodes are at genesis they are considered synced.
            if (node1.FullNode.ChainIndexer.Tip.Height == 0 && node2.FullNode.ChainIndexer.Tip.Height == 0)
                return true;

            if (node1.FullNode.ChainIndexer.Tip.HashBlock != node2.FullNode.ChainIndexer.Tip.HashBlock)
                return false;

            if (node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock)
                return false;

            // Check that node1 tip exists in node2 store (either in disk or in the pending list)
            if (node1.FullNode.BlockStore().GetBlock(node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            // Check that node2 tip exists in node1 store (either in disk or in the pending list)
            if (node2.FullNode.BlockStore().GetBlock(node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) == null)
                return false;

            if (!ignoreMempool)
            {
                if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count)
                    return false;
            }

            return true;
        }

        public static async Task BroadcastTransactionAsync(this CoreNode node, Transaction transaction)
        {
            var broadcasterManager = node.FullNode.NodeService<IBroadcasterManager>();
            await broadcasterManager.BroadcastTransactionAsync(transaction);
        }

        /// <summary>
        /// Used to instantiate a CA client with the admin's credentials. If multiple nodes need to interact with the CA in a test, they will need their own accounts & clients created.
        /// </summary>
        public static CaClient GetAdminClient()
        {
            var httpClient = new HttpClient();
            return new CaClient(new Uri(BaseAddress), httpClient, Settings.AdminAccountId, CaTestHelper.AdminPassword);
        }

        /// <summary>
        /// Creates a new account against the supplied running CA from scratch, and returns the client for it.
        /// </summary>
        public static CaClient GetClient(IWebHost server, List<string> requestedPermissions = null)
        {
            var httpClient = new HttpClient();
            CredentialsModel credentials = CaTestHelper.CreateAccount(server, AccountAccessFlags.AdminAccess, permissions: requestedPermissions);
            return new CaClient(new Uri(BaseAddress), httpClient, credentials.AccountId, credentials.Password);
        }

        /// <summary>
        /// Returns the CA certificate that is stored on an initialized CA server.
        /// </summary>
        public static X509Certificate GetCertificateFromInitializedCAServer(IWebHost server)
        {
            var settings = (Settings)server.Services.GetService(typeof(Settings));
            var acLocation = Path.Combine(settings.DataDirectory, CaCertificatesManager.CaCertFilename);
            var certParser = new X509CertificateParser();
            return certParser.ReadCertificate(File.ReadAllBytes(acLocation));
        }

        public static void RevokeCertificateFromInitializedCAServer(IWebHost server)
        {
            var caCertificatesManager = (CaCertificatesManager)server.Services.GetService(typeof(CaCertificatesManager));

            // Get a good cetificate.
            var certs = caCertificatesManager.GetAllCertificates(new CredentialsAccessModel(1, CaTestHelper.AdminPassword, AccountAccessFlags.AccessAnyCertificate));

            var model = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(new CredentialsModelWithThumbprintModel()
            {
                AccountId = 1,
                Password = CaTestHelper.AdminPassword,
                Thumbprint = certs[0].Thumbprint
            }, AccountAccessFlags.RevokeCertificates);

            caCertificatesManager.RevokeCertificate(model);
        }

        public static string GetDataFolderName([CallerMemberName] string callingMethod = null)
        {
            // Create a datafolder path for the CA settings to use
            string hash = Guid.NewGuid().ToString("N").Substring(0, 7);
            string numberedFolderName = string.Join(
                ".",
                new[] { hash }.Where(s => s != null));
            return Path.Combine(Path.GetTempPath(), callingMethod, numberedFolderName);
        }

        public static IWebHostBuilder CreateWebHostBuilder(string dataFolderName)
        {
            var settings = new Settings();
            settings.Initialize(new string[] { $"-datadir={dataFolderName}", $"-serverurls={BaseAddress}" });

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<TestOnlyStartup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });

            return builder;
        }

        public static Transaction CreateContractCreateTransaction(CoreNode node, Key key)
        {
            Transaction transaction = Network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessSimpleContract.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(Network));

            return transaction;
        }

        public static Transaction CreateContractCallTransaction(CoreNode node, uint160 address, Key key)
        {
            Transaction transaction = Network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, address, "CallMe");
            byte[] outputScript = node.FullNode.NodeService<ICallDataSerializer>().Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(Network));

            return transaction;
        }

        public static Transaction CreateBasicOpReturnTransaction(CoreNode node)
        {
            Transaction transaction = Network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            ITokenlessSigner signer = node.FullNode.NodeService<ITokenlessSigner>();
            signer.InsertSignedTxIn(transaction, node.TransactionSigningPrivateKey.GetBitcoinSecret(Network));

            return transaction;
        }
    }
}
