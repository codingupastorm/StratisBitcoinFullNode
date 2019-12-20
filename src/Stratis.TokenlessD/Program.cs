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
using Stratis.Bitcoin.Features.PoA;
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
                var network = new TokenlessNetwork();
                var nodeSettings = new NodeSettings(network, args: args);
                var walletSettings = new TokenlessWalletSettings(nodeSettings);
                var password = nodeSettings.ConfigReader.GetOrDefault<string>("password", null);

                bool stopNode = false;

                if (!File.Exists(Path.Combine(nodeSettings.DataFolder.RootPath, TokenlessWalletManager.WalletFileName)))
                {
                    var walletManager = new TokenlessWalletManager(network, nodeSettings.DataFolder, walletSettings);

                    var strMnemonic = nodeSettings.ConfigReader.GetOrDefault<string>("mnemonic", null);

                    if (password == null)
                    {
                        Console.WriteLine($"Run this daemon with a -password=<password> argument so that the wallet file ({TokenlessWalletManager.WalletFileName}) can be created.");
                        Console.WriteLine($"If you are re-creating a wallet then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                        return;
                    }

                    TokenlessWallet wallet;
                    Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                    (wallet, mnemonic) = walletManager.CreateWallet(password, password, mnemonic);

                    Console.WriteLine($"The wallet file ({TokenlessWalletManager.WalletFileName}) has been created.");
                    Console.WriteLine($"Record the mnemonic ({mnemonic}) in a safe place.");
                    Console.WriteLine($"IMPORTANT: You will need the mnemonic to recover the wallet.");

                    stopNode = true;
                }

                if (!File.Exists(Path.Combine(nodeSettings.DataFolder.RootPath, KeyTool.KeyFileDefaultName)))
                {
                    if (password == null)
                    {
                        Console.WriteLine($"Run this daemon with a -password=<password> argument so that the federation key ({KeyTool.KeyFileDefaultName}) can be created.");
                        return;
                    }

                    var walletManager = new TokenlessWalletManager(network, nodeSettings.DataFolder, walletSettings);
                    Key key = walletManager.GetExtKey(password, TokenlessWalletAccount.BlockSigning).PrivateKey;
                    var keyTool = new KeyTool(nodeSettings.DataFolder);
                    keyTool.SavePrivateKey(key);

                    Console.WriteLine($"The federation key ({KeyTool.KeyFileDefaultName}) has been created.");
                    stopNode = true;
                }

                if (!File.Exists(Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.ClientCertificateName)))
                {
                    if (password == null)
                    {
                        Console.WriteLine($"Run this daemon with a -password=<password> argument so that the client certificate ({CertificatesManager.ClientCertificateName}) can be requested.");
                        return;
                    }

                    // TODO: 4693 - Generate certificate request.
                }
                else
                {
                    // TODO: 4693 - Generate certificate request.
                }

                if (stopNode)
                {
                    Console.WriteLine($"Restart the daemon.");
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