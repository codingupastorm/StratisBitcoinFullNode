using System;
using System.IO;
using System.Runtime.CompilerServices;
using CertificateAuthority;
using CertificateAuthority.Models;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
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

        public (CoreNode, Key) CreateFullTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate authorityCertificate, CaClient client)
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
                // TODO: Similar problem to the TokenlessD daemon - need to somehow supply a CertificatesManager here
                //var walletManager = new TokenlessWalletManager(network, settings.DataFolder, new TokenlessWalletSettings(settings));
                //walletManager.Initialize();

                var tool = new KeyTool(settings.DataFolder);
                tool.SavePrivateKey(network.FederationKeys[nodeIndex], KeyType.FederationKey);

                Key clientCertificatePrivateKey = walletManager.GetExtKey("test", TokenlessWalletAccount.BlockSigning).PrivateKey;
                PubKey pubKey = clientCertificatePrivateKey.PubKey;
                BitcoinPubKeyAddress address = pubKey.GetAddress(network);
                X509Certificate clientCertificate = IssueCertificate(client, clientCertificatePrivateKey, pubKey, address);

                Assert.NotNull(clientCertificate);

                if (authorityCertificate != null && clientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.GetEncoded());
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(clientCertificate, clientCertificatePrivateKey, "test"));
                }

                return (node, clientCertificatePrivateKey);
            }
        }
        private X509Certificate IssueCertificate(CaClient client, Key privKey, PubKey pubKey, BitcoinPubKeyAddress address)
        {
            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString());

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
