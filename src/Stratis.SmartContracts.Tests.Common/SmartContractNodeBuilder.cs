﻿using System;
using System.IO;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
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

        public CoreNode CreateTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client, string isInfraNode = "0", bool initialRun = true)
        {
            string dataFolder = this.GetNextDataFolderName(nodeIndex: nodeIndex);

            string commonName = CaTestHelper.GenerateRandomString();

            var configParameters = new NodeConfigParameters()
            {
                { "caurl" , "http://localhost:5050" },
                { "isinfranode", isInfraNode }
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
                TokenlessKeyStoreManager keyStoreManager = InitializeNodeKeyStore(node, network, nodeSettings);

                BitcoinPubKeyAddress address = node.ClientCertificatePrivateKey.PubKey.GetAddress(network);
                Key miningKey = keyStoreManager.GetKey("test", TokenlessWalletAccount.BlockSigning);

                if (!initialRun)
                    return node;

                (X509Certificate x509, CertificateInfoModel CertificateInfo) = IssueCertificate(client, node.ClientCertificatePrivateKey, node.TransactionSigningPrivateKey.PubKey, address, miningKey.PubKey);
                node.ClientCertificate = CertificateInfo;
                Assert.NotNull(node.ClientCertificate);

                if (authorityCertificate != null && node.ClientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(x509, node.ClientCertificatePrivateKey, "test"));
                }

                return node;
            }
        }

        public CoreNode CreateChannelNode(CoreNode infraNode, string channelName, CaClient client, int nodeIndex = 0)
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

            // Copy the parent node's authority certificate to the channel node's root.
            File.Copy(Path.Combine(infraNode.FullNode.Settings.DataDir, CertificatesManager.AuthorityCertificateName), Path.Combine(channelRootFolder, CertificatesManager.AuthorityCertificateName));

            // Initialize the channel node's keystore to get the client certificate private and transaction signing key etc.
            string[] args = new string[] { "-password=test", };
            var nodeSettings = new NodeSettings(channelNetwork, args: args);
            TokenlessKeyStoreManager keyStoreManager = InitializeNodeKeyStore(channelNode, channelNetwork, nodeSettings);

            BitcoinPubKeyAddress address = channelNode.ClientCertificatePrivateKey.PubKey.GetAddress(channelNetwork);
            Key miningKey = keyStoreManager.GetKey("test", TokenlessWalletAccount.BlockSigning);

            // Issue a certificate for the channel node and write it to the root folder of the node.
            (X509Certificate x509, CertificateInfoModel CertificateInfo) = IssueCertificate(client, channelNode.ClientCertificatePrivateKey, channelNode.TransactionSigningPrivateKey.PubKey, address, miningKey.PubKey);
            channelNode.ClientCertificate = CertificateInfo;
            Assert.NotNull(infraNode.ClientCertificate);
            File.WriteAllBytes(Path.Combine(channelRootFolder, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(x509, channelNode.ClientCertificatePrivateKey, "test"));

            NetworkRegistration.Clear();

            return channelNode;
        }

        public CoreNode CreateInfraNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client)
        {
            return CreateTokenlessNode(network, nodeIndex, authorityCertificate, client, "1");
        }

        private TokenlessKeyStoreManager InitializeNodeKeyStore(CoreNode node, Network network, NodeSettings nodeSettings)
        {
            var loggerFactory = new LoggerFactory();
            var revocationChecker = new RevocationChecker(nodeSettings, null, loggerFactory, DateTimeProvider.Default);
            var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
            var keyStoreManager = new TokenlessKeyStoreManager(network, nodeSettings.DataFolder, new TokenlessWalletSettings(nodeSettings), certificatesManager, loggerFactory);

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

            var certParser = new X509CertificateParser();

            return (certParser.ReadCertificate(certificateInfo.CertificateContentDer), certificateInfo);
        }

        public static SmartContractNodeBuilder Create(string testRootFolder)
        {
            string testFolderPath = Path.Combine(testRootFolder, "node");
            var builder = new SmartContractNodeBuilder(testFolderPath);
            return builder;
        }
    }
}
