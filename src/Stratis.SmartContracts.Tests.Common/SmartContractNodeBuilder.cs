using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.IntegrationTests.Common;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless;
using Stratis.SmartContracts.Networks;

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
            tool.SavePrivateKey(network.FederationKeys[nodeIndex]);

            return node;
        }

        public CoreNode CreateFullTokenlessNode(TokenlessNetwork network, int nodeIndex, X509Certificate2 authorityCertificate = null, X509Certificate2 clientCertificate = null)
        {
            // TODO: Shouldn't need default params. Just helps soln build.
            string dataFolder = this.GetNextDataFolderName();

            CoreNode node = this.CreateNode(new FullTokenlessRunner(dataFolder, network, this.TimeProvider), "poa.conf");

            using (var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder }))
            {
                var tool = new KeyTool(settings.DataFolder);
                tool.SavePrivateKey(network.FederationKeys[nodeIndex]);

                if (authorityCertificate != null && clientCertificate != null)
                {
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.AuthorityCertificateName), authorityCertificate.RawData);
                    File.WriteAllBytes(Path.Combine(settings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), clientCertificate.RawData);
                }

                return node;
            }
        }

        public CoreNode CreateWhitelistedContractPoANode(SmartContractsPoAWhitelistRegTest network, int nodeIndex)
        {
            string dataFolder = this.GetNextDataFolderName();

            CoreNode node = this.CreateNode(new WhitelistedContractPoARunner(dataFolder, network, this.TimeProvider), "poa.conf");
            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });

            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(network.FederationKeys[nodeIndex]);
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
