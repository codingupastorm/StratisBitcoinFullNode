using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Core.Configuration.Settings;
using Stratis.Core.Networks;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeConfiguration
{
    public class ConsensusSettingsTest
    {
        private readonly Network testNet = new BitcoinTest();

        [Fact]
        public void LoadConfigWithAssumeValidHexLoads()
        {
            var validHexBlock = new uint256("00000000229d9fb87182d73870d53f9fdd9b76bfc02c059e6d9a6c7a3507031d");
            var nodeSettings = new NodeSettings(this.testNet, args: new string[] { $"-assumevalid={validHexBlock.ToString()}" });
            var settings = new ConsensusSettings(nodeSettings);
            Assert.Equal(validHexBlock, settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithAssumeValidZeroSetsToNull()
        {
            var nodeSettings = new NodeSettings(this.testNet, args: new string[] { "-assumevalid=0" });
            var settings = new ConsensusSettings(nodeSettings);
            Assert.Null(settings.BlockAssumedValid);
        }

        [Fact]
        public void LoadConfigWithInvalidAssumeValidThrowsConfigException()
        {
            var nodeSettings = new NodeSettings(this.testNet, args: new string[] { "-assumevalid=xxx" });
            Assert.Throws<ConfigurationException>(() => new ConsensusSettings(nodeSettings));
        }

        [Fact]
        public void LoadConfigWithDefaultsSetsToNetworkDefault()
        {
            Network network = new StratisMain();
            var settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = new StratisTest();
            settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            network = new BitcoinMain();
            settings = new ConsensusSettings(NodeSettings.Default(network));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);

            settings = new ConsensusSettings(NodeSettings.Default(this.testNet));
            Assert.Equal(network.Consensus.DefaultAssumeValid, settings.BlockAssumedValid);
        }
    }
}
