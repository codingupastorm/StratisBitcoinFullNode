using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.TokenlessD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                // TODO: Fix this - providing a working CertificatesManager to the wallet is non-trivial
                var network = new TokenlessNetwork();
                var nodeSettings = new NodeSettings(network, args: args);
                var loggerFactory = new LoggerFactory();
                var revocationChecker = new RevocationChecker(nodeSettings, null, loggerFactory, new DateTimeProvider());
                var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
                var walletManager = new TokenlessWalletManager(network, nodeSettings.DataFolder, new TokenlessWalletSettings(nodeSettings), certificatesManager);
                if (!walletManager.Initialize())
                    return;

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseTokenlessPoaConsenus(network)
                    .UseMempool()
                    .UseTokenlessWallet()
                    .UseApi()
                    .AddRPC()
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
                Console.WriteLine("There was a problem initializing the node. Details: '{0}'", ex.ToString());
            }
        }
    }
}