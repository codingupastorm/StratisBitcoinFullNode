using System;
using System.Threading.Tasks;
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

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UseTokenlessPoaConsenus(network)
                    .UseMempool()
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