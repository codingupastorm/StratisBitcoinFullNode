using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CertificateAuthority;
using CertificateAuthority.Models;
using CertificateAuthority.Tests.Common;
using MembershipServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.PoA;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Core.AccessControl;
using Xunit;

namespace Stratis.SmartContracts.Tests.Common
{
    public class SmartContractNodeBuilder : NodeBuilder
    {
        private int lastSystemChannelApiPort;
        private int lastSystemChannelProtocolPort;

        // This does not have to be re-retrieved from the CA for every node.
        private X509Certificate authorityCertificate;

        public EditableTimeProvider TimeProvider { get; }

        public SmartContractNodeBuilder(string rootFolder) : base(rootFolder)
        {
            // We have to override them so that the channel daemons can use 30002 and up.
            var systemChannelNetwork = new SystemChannelNetwork();
            this.lastSystemChannelApiPort = systemChannelNetwork.DefaultAPIPort + 1;
            this.lastSystemChannelProtocolPort = systemChannelNetwork.DefaultPort + 1;

            this.TimeProvider = new EditableTimeProvider();
        }

        private CoreNode CreateCoreNode(
            TokenlessNetwork network,
            int nodeIndex,
            IWebHost server,
            string agent,
            bool isInfraNode,
            bool isSystemNode,
            bool willStartChannels,
            bool initialRun,
            int? apiPortOverride = null,
            int? protocolPortOverride = null,
            string organisation = null,
            List<string> permissions = null,
            bool debugChannels = false,
            NodeConfigParameters configParameters = null)
        {
            string dataFolder = this.GetNextDataFolderName(nodeIndex: nodeIndex);

            string commonName = CaTestHelper.GenerateRandomString();

            IServerAddressesFeature serverAddresses = server.ServerFeatures.Get<IServerAddressesFeature>();
            string caBaseAddress = serverAddresses.Addresses.First();

            if (configParameters == null)
                configParameters = new NodeConfigParameters();

            if (!configParameters.ContainsKey("caurl"))
                configParameters.Add("caurl", caBaseAddress);

            if (!configParameters.ContainsKey("debug"))
                configParameters.Add("debug", "1");

            if (isInfraNode)
            {
                configParameters.Add("isinfranode", "True");

                if (apiPortOverride != null)
                    configParameters.Add("systemchannelapiport", apiPortOverride.ToString());

                if (protocolPortOverride != null)
                    configParameters.Add("systemchannelprotocolport", protocolPortOverride.ToString());
            }

            if (isSystemNode)
                configParameters.Add("issystemchannelnode", "True");

            if (willStartChannels)
            {
                configParameters.Add("channelprocesspath", "..\\..\\..\\..\\Stratis.TokenlessD\\");

                // We need set this to true for all integration tests that start channel daemons as dotnet.exe is called differently.
                configParameters.Add("projectmode", "True");
            }

            CoreNode node = this.CreateNode(new TokenlessNodeRunner(dataFolder, network, this.TimeProvider, agent, debugChannels ? this : null), "poa.conf", configParameters: configParameters);

            // If not a federation member then generate a mnemonic.
            if (!configParameters.TryGetValue("mnemonic", out string mnemonic))
            {
                mnemonic = (nodeIndex < TokenlessNetwork.Mnemonics.Length
                    ? TokenlessNetwork.Mnemonics[nodeIndex]
                    : new Mnemonic(Wordlist.English, WordCount.Twelve)).ToString();
            }

            if (nodeIndex < network.FederationKeys.Length)
                Guard.Equals(network.FederationKeys[nodeIndex], TokenlessNetwork.FederationKeyFromMnemonic(new Mnemonic(mnemonic)).PubKey);

            string[] args = initialRun ?
                new string[] {
                    "-conf=poa.conf",
                    "-datadir=" + dataFolder,
                    $"{TokenlessKeyStoreSettings.KeyStorePasswordKey}=test",
                    $"-mnemonic={ mnemonic }",
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
                };

            using (var settings = new NodeSettings(network, args: args))
            {
                var dataFolderRootPath = Path.Combine(dataFolder, network.RootFolderName, network.Name);

                if (this.authorityCertificate == null)
                    this.authorityCertificate = TokenlessTestHelper.GetCertificateFromInitializedCAServer(server);

                File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificateAuthorityInterface.AuthorityCertificateName), this.authorityCertificate.GetEncoded());

                TokenlessKeyStoreManager keyStoreManager = InitializeNodeKeyStore(node, network, settings);

                BitcoinPubKeyAddress address = node.ClientCertificatePrivateKey.PubKey.GetAddress(network);
                Key miningKey = keyStoreManager.GetKey("test", TokenlessKeyStoreAccount.BlockSigning);

                if (!initialRun)
                    return node;

                node.AuthorityCertificate = this.authorityCertificate;

                CaClient client = TokenlessTestHelper.GetClientAndCreateAccount(server, requestedPermissions: permissions, organisation: organisation);

                (X509Certificate x509, CertificateInfoModel CertificateInfo) = IssueCertificate(client, node.ClientCertificatePrivateKey, node.TransactionSigningPrivateKey.PubKey, address, miningKey.PubKey);

                node.ClientCertificate = CertificateInfo;
                Assert.NotNull(node.ClientCertificate);

                if (this.authorityCertificate != null && node.ClientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(dataFolderRootPath, CertificateAuthorityInterface.ClientCertificateName), CaCertificatesManager.CreatePfx(x509, node.ClientCertificatePrivateKey, "test"));

                    var localMsdConfiguration = new LocalMembershipServicesConfiguration(settings.DataDir, network);
                    localMsdConfiguration.InitializeFolderStructure();

                    // Put certificate into applicable local MSD folder
                    var ownCertificatePath = Path.Combine(localMsdConfiguration.GetCertificatePath(MemberType.SelfSign, x509));
                    File.WriteAllBytes(ownCertificatePath, x509.GetEncoded());
                }

                return node;
            }
        }

        /// <summary>
        /// This creates a standard (normal) node on the <see cref="TokenlessNetwork"/>.
        /// </summary>
        public CoreNode CreateTokenlessNode(TokenlessNetwork network, int nodeIndex, IWebHost server, string organisation = null, bool initialRun = true, List<string> permissions = null,
            string agent = "tokenless", bool debugChannels = false, NodeConfigParameters configParameters = null)
        {
            return CreateCoreNode(network, nodeIndex, server, agent, false, false, false, initialRun, organisation: organisation, permissions: permissions, debugChannels: debugChannels, configParameters: configParameters);
        }

        /// <summary>
        /// This creates a standard (normal) node on the <see cref="TokenlessNetwork"/> that is also apart of other channels.
        /// </summary>
        public CoreNode CreateTokenlessNodeWithChannels(TokenlessNetwork network, int nodeIndex, IWebHost server, bool initialRun = true, string organisation = null,
            bool debugChannels = false, string agent = "tokenless", NodeConfigParameters configParameters = null)
        {
            return CreateCoreNode(network, nodeIndex, server, agent, false, false, true, initialRun, organisation: organisation, debugChannels: debugChannels, configParameters: configParameters);
        }

        /// <summary>
        /// This creates a "infra" node on the <see cref="TokenlessNetwork"/> which will start a system channel node internally.
        /// </summary>
        public CoreNode CreateInfraNode(TokenlessNetwork network, int nodeIndex, IWebHost server, bool debugChannels = false)
        {
            CoreNode node = CreateCoreNode(network, nodeIndex, server, "infra", true, false, true, true, this.lastSystemChannelApiPort, this.lastSystemChannelProtocolPort, debugChannels: debugChannels);
            this.lastSystemChannelApiPort += 1;
            this.lastSystemChannelProtocolPort += 1;
            return node;
        }

        public ChannelDefinition CreateChannel(CoreNode parentNode, string channelName, int nodeIndex, AccessControlList acl = null)
        {
            if (acl == null)
            {
                acl = new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        CaTestHelper.TestOrganisation
                    }
                };
            }

            // Serialize the channel network and write the json to disk.
            ChannelNetwork channelNetwork = SystemChannelNetwork.CreateChannelNetwork(channelName, channelName.Substring(0, 4), "channels", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            channelNetwork.Id = nodeIndex;
            channelNetwork.InitialAccessList = acl;
            channelNetwork.DefaultAPIPort += nodeIndex;
            var serializedJson = JsonSerializer.Serialize(channelNetwork);

            var channelRootFolder = Path.Combine(parentNode.FullNode.Settings.DataDir, channelNetwork.RootFolderName, channelName);
            Directory.CreateDirectory(channelRootFolder);

            var serializedNetworkFileName = Path.Combine(channelRootFolder, $"{channelName}_network.json");
            File.WriteAllText(serializedNetworkFileName, serializedJson);

            // Save the channel definition so that it can loaded on node start.
            IChannelRepository channelRepository = parentNode.FullNode.NodeService<IChannelRepository>();
            var channelDef = new ChannelDefinition()
            {
                Id = nodeIndex,
                Name = channelName,
                AccessList = channelNetwork.InitialAccessList,
                NetworkJson = serializedJson
            };
            channelRepository.SaveChannelDefinition(channelDef);

            return channelDef;
        }

        public CoreNode CreateChannelNode(string channelRootFolder, params string[] channelArgs)
        {
            // Welcome to Hack City

            // Create a new Channel Runner
            var channelNodeRunner = new ChannelNodeRunner(channelRootFolder, this.TimeProvider);

            // Make a CoreNode out of it. In the case of a channels node this does very little for us! The important thing it does do is get a unique port and apiport.
            CoreNode node = this.CreateNode(channelNodeRunner);

            // Append the ports we generated to the node's arguments. They won't be in here otherwise because CoreNode saves them to some other config file.
            channelArgs = channelArgs.Concat(new string[]
            {
                $"-apiport={node.ApiPort}",
                $"-port={node.ProtocolPort}"
            }).ToArray();

            // Set these args on the runner that we created earlier. We couldn't put them in the constructor because we didn't have the ports yet.
            channelNodeRunner.Args = channelArgs;

            return node;
        }

        private TokenlessKeyStoreManager InitializeNodeKeyStore(CoreNode node, Network network, NodeSettings settings)
        {
            var keyStoreManager = new TokenlessKeyStoreManager(network, settings.DataFolder, new ChannelSettings(settings.ConfigReader), new TokenlessKeyStoreSettings(settings), settings.LoggerFactory);

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
