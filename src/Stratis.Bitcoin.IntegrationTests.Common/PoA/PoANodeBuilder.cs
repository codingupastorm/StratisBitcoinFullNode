using System.IO;
using System.Runtime.CompilerServices;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Configuration;
using Stratis.Features.PoA;

namespace Stratis.Bitcoin.IntegrationTests.Common.PoA
{
    public class PoANodeBuilder : NodeBuilder
    {
        public EditableTimeProvider TimeProvider { get; }

        private PoANodeBuilder(string rootFolder) : base(rootFolder)
        {
            this.TimeProvider = new EditableTimeProvider();
        }

        public static PoANodeBuilder CreatePoANodeBuilder(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            PoANodeBuilder builder = new PoANodeBuilder(testFolderPath);
            builder.WithLogsDisabled();

            return builder;
        }

        public CoreNode CreatePoANode(PoANetwork network)
        {
            return this.CreateNode(new PoANodeRunner(this.GetNextDataFolderName(), network, this.TimeProvider), "poa.conf");
        }

        public CoreNode CreatePoANode(PoANetwork network, string authorityCertificatePath, string clientCertificatePath)
        {
            string dataFolder = this.GetNextDataFolderName();

            var config = new NodeConfigParameters
            {
                { "-capassword", "4815162342" },
                { "-caaccountid", "1" }
            };

            CoreNode node = this.CreateNode(new PoANodeRunner(dataFolder, network, this.TimeProvider), "poa.conf", configParameters: config);

            // Ensures that the network-specific folder-structure is created.
            new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });

            File.Copy(authorityCertificatePath, Path.Combine(dataFolder, "poa", network.Name, Path.GetFileName(authorityCertificatePath)));
            File.Copy(clientCertificatePath, Path.Combine(dataFolder, "poa", network.Name, Path.GetFileName(clientCertificatePath)));

            return node;
        }

        public CoreNode CreatePoANode(PoANetwork network, Key key)
        {
            string dataFolder = this.GetNextDataFolderName();
            CoreNode node = this.CreateNode(new PoANodeRunner(dataFolder, network, this.TimeProvider), "poa.conf");

            var settings = new NodeSettings(network, args: new string[] { "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key, KeyType.FederationKey);

            return node;
        }

        public CoreNode CreatePoANode(PoANetwork network, Key key, string authorityCertificatePath, string clientCertificatePath)
        {
            string dataFolder = this.GetNextDataFolderName();

            var config = new NodeConfigParameters
            {
                { "-capassword", "4815162342" },
                { "-caaccountid", "1" }
            };

            CoreNode node = this.CreateNode(new PoANodeRunner(dataFolder, network, this.TimeProvider), "poa.conf", configParameters: config);

            var settings = new NodeSettings(network, args: new string[] { "-conf=poa.conf", "-datadir=" + dataFolder });
            var tool = new KeyTool(settings.DataFolder);
            tool.SavePrivateKey(key, KeyType.FederationKey);

            File.Copy(authorityCertificatePath, Path.Combine(dataFolder, "poa", network.Name, Path.GetFileName(authorityCertificatePath)));
            File.Copy(clientCertificatePath, Path.Combine(dataFolder, "poa", network.Name, Path.GetFileName(clientCertificatePath)));

            return node;
        }
    }
}
