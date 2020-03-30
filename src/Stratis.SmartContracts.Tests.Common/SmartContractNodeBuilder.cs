﻿using System;
using System.IO;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Networks;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
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

        public CoreNode CreateTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client, bool isInfraNode = false, bool initialRun = true)
        {
            string dataFolder = this.GetNextDataFolderName(nodeIndex: nodeIndex);

            string commonName = CaTestHelper.GenerateRandomString();

            var configParameters = new NodeConfigParameters()
            {
                { "caurl" , "http://localhost:5050" },
                { "isinfranode", isInfraNode.ToString() }
            };

            CoreNode node = this.CreateNode(new TokenlessNodeRunner(dataFolder, network, this.TimeProvider), "poa.conf", configParameters: configParameters);

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

            using (var nodeSettings = new NodeSettings(network, args: args))
            {
                var loggerFactory = new LoggerFactory();
                var revocationChecker = new RevocationChecker(new MembershipServicesDirectory(settings));
                var certificatesManager = new CertificatesManager(settings.DataFolder, settings, loggerFactory, revocationChecker, network);
                var walletManager = new TokenlessWalletManager(network, settings.DataFolder, new TokenlessWalletSettings(settings), certificatesManager, loggerFactory);

                walletManager.Initialize();

                TokenlessWalletManager keyStoreManager = InitializeNodeKeyStore(node, network, nodeSettings);

                BitcoinPubKeyAddress address = node.ClientCertificatePrivateKey.PubKey.GetAddress(network);
                Key miningKey = keyStoreManager.GetKey("test", TokenlessWalletAccount.BlockSigning);

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
                    var ownCertificatePath = Path.Combine(settings.DataDir, LocalMembershipServicesConfiguration.SignCerts, MembershipServicesDirectory.GetCertificateThumbprint(issueResult.x509));
                    File.WriteAllBytes(ownCertificatePath, issueResult.x509.GetEncoded());
                    File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(x509, node.ClientCertificatePrivateKey, "test"));
                }

                return node;
            }
        }

        public CoreNode CreateChannelNode(CoreNode infraNode, string channelName, int nodeIndex = 0)
        {
            // Serialize the channel network and write the json to disk.
            var nodeRootFolder = Path.Combine(this.rootFolder, nodeIndex.ToString());
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork(channelName, "channels");
            var serializedJson = JsonSerializer.Serialize(channelNetwork);

            var channelRootFolder = Path.Combine(nodeRootFolder, channelNetwork.RootFolderName);
            Directory.CreateDirectory(channelRootFolder);

            var serializedNetworkFileName = $"{channelRootFolder}\\{channelName}_network.json";
            File.WriteAllText(serializedNetworkFileName, serializedJson);

            // Create the channel node runner.
            CoreNode channelNode = this.CreateNode(new ChannelNodeRunner(channelName, nodeRootFolder, this.TimeProvider), "poa.conf");

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
            return CreateTokenlessNode(network, nodeIndex, authorityCertificate, client, true);
        }

        private TokenlessWalletManager InitializeNodeKeyStore(CoreNode node, Network network, NodeSettings nodeSettings)
        {
            var loggerFactory = new LoggerFactory();
            var revocationChecker = new RevocationChecker(nodeSettings, null, loggerFactory, DateTimeProvider.Default);
            var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
            var keyStoreManager = new TokenlessWalletManager(network, nodeSettings.DataFolder, new TokenlessWalletSettings(nodeSettings), certificatesManager, loggerFactory);

            keyStoreManager.Initialize();

            node.ClientCertificatePrivateKey = keyStoreManager.GetKey("test", TokenlessWalletAccount.P2PCertificates);
            node.TransactionSigningPrivateKey = keyStoreManager.GetKey("test", TokenlessWalletAccount.TransactionSigning);

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
