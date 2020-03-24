using System;
using System.IO;
using System.Runtime.CompilerServices;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Xunit;

namespace Stratis.SmartContracts.Tests.Common
{
    public class SmartContractNodeBuilder : NodeBuilder
    {
        public EditableTimeProvider TimeProvider { get; }

        public SmartContractNodeBuilder(string rootFolder) : base(rootFolder)
        {
            this.TimeProvider = new EditableTimeProvider();
        }

        public CoreNode CreateFullTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client, bool initialRun = true)
        {
            string dataFolder = this.GetNextDataFolderName(nodeIndex: nodeIndex);

            string commonName = CaTestHelper.GenerateRandomString();

            var configParameters = new NodeConfigParameters()
            {
                { "caurl" , "http://localhost:5050" }
            };

            CoreNode node = this.CreateNode(new FullTokenlessRunner(dataFolder, network, this.TimeProvider), "poa.conf", configParameters: configParameters);

            Mnemonic mnemonic = nodeIndex < 3
                ? TokenlessNetwork.Mnemonics[nodeIndex]
                : new Mnemonic(Wordlist.English, WordCount.Twelve);

            string[] args = initialRun ?
                new string[] {
                    "-conf=poa.conf",
                    "-datadir=" + dataFolder,
                    "-password=test",
                    $"-mnemonic={ mnemonic }",
                    "-certificatepassword=test",
                    "-certificatename=" + commonName,
                    "-certificateorganizationunit=IntegrationTests",
                    "-certificateorganization=Stratis",
                    "-certificatelocality=TestLocality",
                    "-certificatestateorprovince=TestProvince",
                    "-certificateemailaddress=" + commonName + "@example.com",
                    "-certificatecountry=UK"
                } : new string[]
                {
                    "-conf=poa.conf",
                    "-datadir=" + dataFolder,
                    "-certificatepassword=test"
                };

            using (var settings = new NodeSettings(network, args: args))
            {
                var loggerFactory = new LoggerFactory();
                var revocationChecker = new RevocationChecker(new MembershipServicesDirectory(settings));
                var certificatesManager = new CertificatesManager(settings.DataFolder, settings, loggerFactory, revocationChecker, network);
                var walletManager = new TokenlessWalletManager(network, settings.DataFolder, new TokenlessWalletSettings(settings), certificatesManager, loggerFactory);

                walletManager.Initialize();

                node.ClientCertificatePrivateKey = walletManager.GetKey("test", TokenlessWalletAccount.P2PCertificates);
                node.TransactionSigningPrivateKey = walletManager.GetKey("test", TokenlessWalletAccount.TransactionSigning);

                BitcoinPubKeyAddress address = node.ClientCertificatePrivateKey.PubKey.GetAddress(network);

                Key miningKey = walletManager.GetKey("test", TokenlessWalletAccount.BlockSigning);

                if (!initialRun)
                    return node;

                (X509Certificate x509, CertificateInfoModel CertificateInfo) issueResult = IssueCertificate(client, node.ClientCertificatePrivateKey, node.TransactionSigningPrivateKey.PubKey, address, miningKey.PubKey);
                node.ClientCertificate = issueResult.CertificateInfo;
                Assert.NotNull(node.ClientCertificate);

                if (authorityCertificate != null && node.ClientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(issueResult.x509, node.ClientCertificatePrivateKey, "test"));

                    // Put certificate into applicable local MSD folder
                    Directory.CreateDirectory(Path.Combine(settings.DataDir, LocalMembershipServicesConfiguration.SignCerts));
                    var ownCertificatePath = Path.Combine(settings.DataDir, LocalMembershipServicesConfiguration.SignCerts, MembershipServicesDirectory.GetCertificateThumbprint(issueResult.x509));
                    File.WriteAllBytes(ownCertificatePath, issueResult.x509.GetEncoded());
                }

                return node;
            }
        }

        private (X509Certificate, CertificateInfoModel) IssueCertificate(CaClient client, Key privKey, PubKey transactionSigningPubKey, BitcoinPubKeyAddress address, PubKey blockSigningPubKey)
        {
            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(privKey.PubKey.ToBytes()), address.ToString(), Convert.ToBase64String(transactionSigningPubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privKey);

            CertificateInfoModel certificateInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certificateInfo);
            Assert.Equal(address.ToString(), certificateInfo.Address);

            return (certificateInfo.ToCertificate(), certificateInfo);
        }

        public static SmartContractNodeBuilder Create(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            var builder = new SmartContractNodeBuilder(testFolderPath);
            builder.WithLogsDisabled();
            return builder;
        }
    }
}
