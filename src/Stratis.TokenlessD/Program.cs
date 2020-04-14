using System;
using System.Threading.Tasks;
using MembershipServices;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Features.Api;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.TokenlessD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var network = new TokenlessNetwork();
                var nodeSettings = new NodeSettings(network, args: args);
                var loggerFactory = new LoggerFactory();
                var revocationChecker = new RevocationChecker(new MembershipServicesDirectory(nodeSettings));
                var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
                var walletManager = new TokenlessKeyStoreManager(network, nodeSettings.DataFolder, new TokenlessKeyStoreSettings(nodeSettings), certificatesManager, loggerFactory);
                if (!walletManager.Initialize())
                    return;

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseTokenlessPoaConsenus(network)
                    .UseMempool()
                    .UseTokenlessKeyStore()
                    .UseApi(o => o.Exclude<SmartContractFeature>())
                    .AddSmartContracts(options =>
                    {
                        options.UseTokenlessReflectionExecutor();
                        options.UseSmartContractType<TokenlessSmartContract>();
                    })
                    .AsTokenlessNetwork();

                IFullNode node = nodeBuilder.Build();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node : '{0}'", ex.ToString());
            }
        }
    }
}