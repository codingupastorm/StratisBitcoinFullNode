using System;
using System.IO;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
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
                var network = new TokenlessNetwork();
                var nodeSettings = new NodeSettings(network, args: args);
                var walletSettings = new WalletSettings(nodeSettings);

                if (!File.Exists(Path.Combine(nodeSettings.DataFolder.RootPath, WalletManager.WalletFileName)))
                {
                    var walletManager = new WalletManager(network, nodeSettings.DataFolder, walletSettings);

                    var password = nodeSettings.ConfigReader.GetOrDefault<string>("password", null);
                    var strMnemonic = nodeSettings.ConfigReader.GetOrDefault<string>("mnemonic", null);

                    if (password == null)
                    {
                        Console.WriteLine($"Run this daemon with a -password=<password> argument so that the wallet file ({WalletManager.WalletFileName}) can be created.");
                        Console.WriteLine($"If you are re-creating a wallet then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                        return;
                    }

                    Wallet wallet;
                    Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                    (wallet, mnemonic) = walletManager.CreateWallet(password, password, mnemonic);

                    Console.WriteLine($"The wallet file ({WalletManager.WalletFileName}) has been created.");
                    Console.WriteLine($"Record the mnemonic ({mnemonic}) in a safe place.");
                    Console.WriteLine($"IMPORTANT: You will need the mnemonic to recover the wallet.");
                    Console.WriteLine($"Restart the daemon after recording the mnemonic.");
                    return;
                }

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