﻿using System;
using System.IO;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using NBitcoin;
using NBitcoin.Networks;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Features.PoA.ProtocolEncryption;
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

        public CoreNode CreateTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client, string agent = "TKL", bool isInfraNode = false, bool willStartChannels = false, bool initialRun = true)
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
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, "channels", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            channelNetwork.DefaultAPIPort += nodeIndex;
            var serializedJson = JsonSerializer.Serialize(channelNetwork);

            var channelRootFolder = Path.Combine(parentNode.FullNode.Settings.DataDir, channelNetwork.RootFolderName, channelName);
            Directory.CreateDirectory(channelRootFolder);

            var serializedNetworkFileName = $"{channelRootFolder}\\{channelName}_network.json";
            File.WriteAllText(serializedNetworkFileName, serializedJson);

            // Save the channel definition so that it can loaded on node start.
            IChannelRepository channelRepository = parentNode.FullNode.NodeService<IChannelRepository>();
            channelRepository.SaveChannelDefinition(new ChannelDefinition() { Id = nodeIndex, Name = channelName, NetworkJson = serializedJson });
        }

        public CoreNode CreateChannelNode(CoreNode infraNode, string channelName, int nodeIndex)
        {
            // Serialize the channel network and write the json to disk.
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, "channels", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            channelNetwork.DefaultAPIPort += nodeIndex;
            var serializedJson = JsonSerializer.Serialize(channelNetwork);

            var nodeRootFolder = Path.Combine(this.rootFolder, nodeIndex.ToString());
            var channelRootFolder = Path.Combine(nodeRootFolder, channelNetwork.RootFolderName);
            Directory.CreateDirectory(channelRootFolder);

            var serializedNetworkFileName = $"{channelRootFolder}\\{channelName}_network.json";
            File.WriteAllText(serializedNetworkFileName, serializedJson);

            // Create the channel node runner.
            CoreNode channelNode = this.CreateNode(new ChannelNodeRunner(channelName, nodeRootFolder, this.TimeProvider), "channel.conf");

            // Initialize the channel nodes's data folder etc.
            string[] args = new string[] { "-datadir=" + nodeRootFolder, };
            using (var nodeSettings = new NodeSettings(channelNetwork, args: args))
            {
                // Copy the parent node's authority and client certificate to the channel node's root.
                File.Copy(Path.Combine(infraNode.FullNode.Settings.DataDir, CertificatesManager.AuthorityCertificateName), Path.Combine(nodeSettings.DataDir, CertificatesManager.AuthorityCertificateName));
                File.Copy(Path.Combine(infraNode.FullNode.Settings.DataDir, CertificatesManager.ClientCertificateName), Path.Combine(nodeSettings.DataDir, CertificatesManager.ClientCertificateName));

                NetworkRegistration.Clear();
            }

            return channelNode;
        }

        public CoreNode CreateInfraNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client)
        {
            return CreateTokenlessNode(network, nodeIndex, authorityCertificate, client, "system", true, true);
        }

        private TokenlessKeyStoreManager InitializeNodeKeyStore(CoreNode node, Network network, NodeSettings settings)
        {
            var revocationChecker = new RevocationChecker(new MembershipServicesDirectory(settings));
            var certificatesManager = new CertificatesManager(settings.DataFolder, settings, settings.LoggerFactory, revocationChecker, network);
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
