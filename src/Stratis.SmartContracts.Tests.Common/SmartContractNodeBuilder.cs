using System;
using System.IO;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core.AsyncWork;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.PoA.Tests.Common;
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

        public CoreNode CreateTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client,
            string agent = "TKL", bool isInfraNode = false, bool isSystemNode = false, bool willStartChannels = false, bool initialRun = true)
        {
            string dataFolder = this.GetNextDataFolderName(nodeIndex: nodeIndex);

            string commonName = CaTestHelper.GenerateRandomString();

            var configParameters = new NodeConfigParameters()
            {
                { "caurl" , CaTestHelper.BaseAddress },
                { "debug" , "1" },
            };

            if (isInfraNode)
                configParameters.Add("isinfranode", "True");

            if (isSystemNode)
                configParameters.Add("issystemchannelnode", "True");

            if (willStartChannels)
                configParameters.Add("channelprocesspath", "..\\..\\..\\..\\Stratis.TokenlessD\\");

            CoreNode node = this.CreateNode(new TokenlessNodeRunner(dataFolder, network, this.TimeProvider, agent), "poa.conf", configParameters: configParameters);

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
                var dataFolderRootPath = Path.Combine(dataFolder, network.RootFolderName, network.Name);

                TokenlessKeyStoreManager keyStoreManager = InitializeNodeKeyStore(node, network, settings);

                BitcoinPubKeyAddress address = node.ClientCertificatePrivateKey.PubKey.GetAddress(network);
                Key miningKey = keyStoreManager.GetKey("test", TokenlessKeyStoreAccount.BlockSigning);

                if (!initialRun)
                    return node;

                (X509Certificate x509, CertificateInfoModel CertificateInfo) = IssueCertificate(client, node.ClientCertificatePrivateKey, node.TransactionSigningPrivateKey.PubKey, address, miningKey.PubKey);
                node.ClientCertificate = CertificateInfo;
                Assert.NotNull(node.ClientCertificate);

                if (authorityCertificate != null && node.ClientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(x509, node.ClientCertificatePrivateKey, "test"));

                    // Put certificate into applicable local MSD folder
                    Directory.CreateDirectory(Path.Combine(settings.DataDir, LocalMembershipServicesConfiguration.SignCerts));
                    var ownCertificatePath = Path.Combine(settings.DataDir, LocalMembershipServicesConfiguration.SignCerts, MembershipServicesDirectory.GetCertificateThumbprint(x509));
                    File.WriteAllBytes(ownCertificatePath, x509.GetEncoded());
                }

                return node;
            }
        }

        public void CreateChannel(CoreNode parentNode, string channelName, int nodeIndex)
        {
            // Serialize the channel network and write the json to disk.
            ChannelNetwork channelNetwork = SystemChannelNetwork.CreateChannelNetwork(channelName, "channels", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            channelNetwork.Id = nodeIndex;
            channelNetwork.Organisation = CaTestHelper.TestOrganisation;
            channelNetwork.DefaultAPIPort += nodeIndex;
            var serializedJson = JsonSerializer.Serialize(channelNetwork);

            var channelRootFolder = Path.Combine(parentNode.FullNode.Settings.DataDir, channelNetwork.RootFolderName, channelName);
            Directory.CreateDirectory(channelRootFolder);

            var serializedNetworkFileName = $"{channelRootFolder}\\{channelName}_network.json";
            File.WriteAllText(serializedNetworkFileName, serializedJson);

            // Save the channel definition so that it can loaded on node start.
            IChannelRepository channelRepository = parentNode.FullNode.NodeService<IChannelRepository>();
            channelRepository.SaveChannelDefinition(new ChannelDefinition() { Id = nodeIndex, Name = channelName, Organisation = CaTestHelper.TestOrganisation, NetworkJson = serializedJson });
        }

        public CoreNode CreateInfraNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client)
        {
            return CreateTokenlessNode(network, nodeIndex, authorityCertificate, client, "system", isInfraNode: true, willStartChannels: true);
        }

        private TokenlessKeyStoreManager InitializeNodeKeyStore(CoreNode node, Network network, NodeSettings settings)
        {
            var certificatesManager = new CertificatesManager(settings.DataFolder, settings, settings.LoggerFactory, network, new MembershipServicesDirectory(settings));
            var keyStoreManager = new TokenlessKeyStoreManager(network, settings.DataFolder, new ChannelSettings(settings.ConfigReader), new TokenlessKeyStoreSettings(settings), certificatesManager, settings.LoggerFactory);

            keyStoreManager.Initialize();

            node.ClientCertificatePrivateKey = keyStoreManager.GetKey("test", TokenlessKeyStoreAccount.P2PCertificates);
            node.TransactionSigningPrivateKey = keyStoreManager.GetKey("test", TokenlessKeyStoreAccount.TransactionSigning);

            return keyStoreManager;
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

        public static SmartContractNodeBuilder Create(string testRootFolder)
        {
            string testFolderPath = Path.Combine(testRootFolder, "node");
            var builder = new SmartContractNodeBuilder(testFolderPath);
            return builder;
        }
    }
}
