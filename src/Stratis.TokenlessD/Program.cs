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
using Stratis.Bitcoin.Features.SignalR;
using Stratis.Bitcoin.Features.SignalR.Broadcasters;
using Stratis.Bitcoin.Features.SignalR.Events;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.Diagnostic;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Features.Wallet.Tokenless;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.TokenlessD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                // Use TokenlessNetwork.
                var network = new TokenlessNetwork();
                var nodeSettings = new NodeSettings(network, args: args);
                var walletSettings = new DLTWalletSettings(nodeSettings);

                if (!File.Exists(Path.Combine(nodeSettings.DataFolder.RootPath, DLTWalletManager.WalletFileName)))
                {
                    var walletManager = new DLTWalletManager(network, nodeSettings.DataFolder, walletSettings);

                    var password = nodeSettings.ConfigReader.GetOrDefault<string>("password", null);
                    var strMnemonic = nodeSettings.ConfigReader.GetOrDefault<string>("mnemonic", null);

                    if (password == null)
                    {
                        Console.WriteLine($"Run this daemon with a -password=<password> argument so that the wallet file ({DLTWalletManager.WalletFileName}) can be created.");
                        Console.WriteLine($"If you are re-creating a wallet then also pass a -mnemonic=<mnemonic> argument.");
                        return;
                    }

                    DLTWallet wallet;
                    Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                    (wallet, mnemonic) = walletManager.CreateWallet(password, password, mnemonic);

                    Console.WriteLine($"The wallet file ({DLTWalletManager.WalletFileName}) has been created.");
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
                    .AsTokenlessNetwork()
                    .UseDiagnosticFeature();

                if (nodeSettings.EnableSignalR)
                {
                    nodeBuilder.AddSignalR(options =>
                    {
                        options.EventsToHandle = new[]
                        {
                            (IClientEvent) new BlockConnectedClientEvent(),
                            new TransactionReceivedClientEvent()
                        };

                        options.ClientEventBroadcasters = new[]
                        {
                            (Broadcaster: typeof(StakingBroadcaster), ClientEventBroadcasterSettings: new ClientEventBroadcasterSettings
                                {
                                    BroadcastFrequencySeconds = 5
                                }),
                            (Broadcaster: typeof(WalletInfoBroadcaster), ClientEventBroadcasterSettings: new ClientEventBroadcasterSettings
                                {
                                    BroadcastFrequencySeconds = 5
                                })
                        };
                    });
                }

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