using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Core.Base;
using Stratis.Core.Base.Deployments;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

[assembly: InternalsVisibleTo("Stratis.Features.Miner.Tests")]
[assembly: InternalsVisibleTo("Stratis.Features.Consensus.Tests")]

namespace Stratis.Features.Consensus
{
    public class ConsensusFeature : FullNodeFeature
    {
        private readonly IChainState chainState;

        private readonly IConnectionManager connectionManager;

        private readonly IConsensusManager consensusManager;

        private readonly NodeDeployments nodeDeployments;

        public ConsensusFeature(
            Network network,
            IChainState chainState,
            IConnectionManager connectionManager,
            IConsensusManager consensusManager,
            NodeDeployments nodeDeployments)
        {
            this.chainState = chainState;
            this.connectionManager = connectionManager;
            this.consensusManager = consensusManager;
            this.nodeDeployments = nodeDeployments;

            this.chainState.MaxReorgLength = network.Consensus.MaxReorgLength;
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            DeploymentFlags flags = this.nodeDeployments.GetFlags(this.consensusManager.Tip);
            if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                this.connectionManager.AddDiscoveredNodesRequirement(NetworkPeerServices.NODE_WITNESS);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            ConsensusSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ConsensusSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
        }
    }
}