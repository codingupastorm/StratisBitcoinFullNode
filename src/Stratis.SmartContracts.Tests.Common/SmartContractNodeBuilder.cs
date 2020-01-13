using System;
using System.IO;
using System.Runtime.CompilerServices;
using CertificateAuthority;
using CertificateAuthority.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Stratis.SmartContracts.Networks;
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

        public CoreNode CreateSmartContractPoANode(SmartContractsPoARegTest network, int nodeIndex)
        {
            string dataFolder = this.GetNextDataFolderName();

            CoreNode node = this.CreateNode(new SmartContractPoARunner(dataFolder, network, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });

            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(network.FederationKeys[nodeIndex], KeyType.FederationKey);

            return node;
        }

        public (CoreNode, Key, Key) CreateFullTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client)
        {
            string dataFolder = this.GetNextDataFolderName();

            var configParameters = new NodeConfigParameters()
            {
                { "caurl" , "http://localhost:5050" }
            };

            CoreNode node = this.CreateNode(new FullTokenlessRunner(dataFolder, network, this.TimeProvider), "poa.conf", configParameters: configParameters);

            Mnemonic[] mnemonics = {
                    new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom"),
                    new Mnemonic("idle power swim wash diesel blouse photo among eager reward govern menu"),
                    new Mnemonic("high neither night category fly wasp inner kitchen phone current skate hair") };

            using (var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder, "-password=test", $"-mnemonic={ mnemonics[nodeIndex] }", "-certificatepassword=test" }))
            {
                var loggerFactory = new LoggerFactory();
                var revocationChecker = new RevocationChecker(settings, null, loggerFactory, new DateTimeProvider());
                var certificatesManager = new CertificatesManager(settings.DataFolder, settings, loggerFactory, revocationChecker, network);
                var walletManager = new TokenlessWalletManager(network, settings.DataFolder, new TokenlessWalletSettings(settings), certificatesManager);

                walletManager.Initialize();

                var tool = new KeyTool(settings.DataFolder);
                tool.SavePrivateKey(network.FederationKeys[nodeIndex], KeyType.FederationKey);

                Key clientCertificatePrivateKey = walletManager.GetExtKey("test", TokenlessWalletAccount.P2PCertificates).PrivateKey;
                PubKey pubKey = clientCertificatePrivateKey.PubKey;
                Key transactionSigningPrivateKey = walletManager.GetExtKey("test", TokenlessWalletAccount.TransactionSigning).PrivateKey;
                PubKey transactionSigningPubKey = transactionSigningPrivateKey.PubKey;
                BitcoinPubKeyAddress address = pubKey.GetAddress(network);
                PubKey blockSigningPubKey = walletManager.GetExtKey("test", TokenlessWalletAccount.BlockSigning).PrivateKey.PubKey;

                X509Certificate clientCertificate = IssueCertificate(client, clientCertificatePrivateKey, transactionSigningPubKey, address, blockSigningPubKey);

                Assert.NotNull(clientCertificate);

                if (authorityCertificate != null && clientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(clientCertificate, clientCertificatePrivateKey, "test"));
                }

                return (node, clientCertificatePrivateKey, transactionSigningPrivateKey);
            }
        }
        private X509Certificate IssueCertificate(CaClient client, Key privKey, PubKey transactionSigningPubKey, BitcoinPubKeyAddress address, PubKey blockSigningPubKey)
        {
            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(privKey.PubKey.ToBytes()), address.ToString(), Convert.ToBase64String(transactionSigningPubKey.Hash.ToBytes()), Convert.ToBase64String(blockSigningPubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privKey);

            CertificateInfoModel certInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            var certParser = new X509CertificateParser();

            return certParser.ReadCertificate(Convert.FromBase64String(certInfo.CertificateContentDer));
        }

        public CoreNode CreateWhitelistedContractPoANode(SmartContractsPoAWhitelistRegTest network, int nodeIndex)
        {
            string dataFolder = this.GetNextDataFolderName();

            CoreNode node = this.CreateNode(new WhitelistedContractPoARunner(dataFolder, network, this.TimeProvider), "poa.conf");
            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });

            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(network.FederationKeys[nodeIndex], KeyType.FederationKey);
            return node;
        }

        public CoreNode CreateSmartContractPowNode()
        {
            Network network = new SmartContractsRegTest();
            return CreateNode(new StratisSmartContractNode(this.GetNextDataFolderName(), network), "stratis.conf");
        }

        public CoreNode CreateSmartContractPosNode()
        {
            Network network = new SmartContractPosRegTest();
            return CreateNode(new StratisSmartContractPosNode(this.GetNextDataFolderName(), network), "stratis.conf");
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
