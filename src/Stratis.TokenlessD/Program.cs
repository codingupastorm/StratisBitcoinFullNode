using System;
using System.IO;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Core;
using Stratis.Core.Builder;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.Api;
using Stratis.Features.BlockStore;
using Stratis.Features.MemoryPool;
using Stratis.Features.SmartContracts;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.TokenlessD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Program.MainAsync(args).GetAwaiter().GetResult();
        }

        public static async Task MainAsync(string[] args)
        {
            try
            {
                Network network = null;

                // TODO-TL: This needs to be moved someplace else.
                var configReader = new TextFileConfiguration(args);
                var dataDir = configReader.GetOrDefault("datadir", "");

                var configurationFile = configReader.GetOrDefault("conf", "");
                if (!string.IsNullOrEmpty(configurationFile))
                {
                    var configurationFilePath = Path.Combine(dataDir, configurationFile);
                    var fileConfig = new TextFileConfiguration(File.ReadAllText(configurationFilePath));
                    fileConfig.MergeInto(configReader);
                }

                NodeSettings nodeSettings = null;

                var channelSettings = new ChannelSettings(configReader);
                if (channelSettings.IsChannelNode)
                {
                    if (channelSettings.IsSystemChannelNode)
                    {
                        network = new SystemChannelNetwork();
                        nodeSettings = new NodeSettings(network, agent: "Channel-System", configReader: configReader);
                    }
                    else
                    {
                        network = ChannelNetwork.Construct(dataDir, channelSettings.ChannelName);
                        nodeSettings = new NodeSettings(network, agent: $"Channel-{channelSettings.ChannelName}", configReader: configReader);
                    }
                }
                else
                {
                    network = new TokenlessNetwork();
                    nodeSettings = new NodeSettings(network, agent: "Tokenless", configReader: configReader);
                }

                // Only non-channel nodes will need to go through the key store initialization process.
                if (!channelSettings.IsChannelNode)
                {
                    var keyStoreManager = new TokenlessKeyStoreManager(network, nodeSettings.DataFolder, channelSettings, new TokenlessKeyStoreSettings(nodeSettings), nodeSettings.LoggerFactory);
                    if (!keyStoreManager.Initialize())
                        return;
                }

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