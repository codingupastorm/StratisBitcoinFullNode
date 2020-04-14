using System.Collections.Generic;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests.Channels
{
    public class ChannelRepositoryTests : LogsTestBase
    {
        public ChannelRepositoryTests() : base(new TokenlessNetwork())
        {

        }

        [Fact]
        public void CanPersistAndReadBackChannelDefinitions()
        {
            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new ChannelKeyValueStore(repositorySerializer, dataFolder, this.LoggerFactory.Object, DateTimeProvider.Default);

            var channelRepository = new ChannelRepository(this.Network, this.LoggerFactory.Object, keyValueStore, repositorySerializer);

            var request1 = new ChannelDefinition()
            {
                Name = "name1"
            };

            channelRepository.SaveChannelDefinition(request1);

            var request2 = new ChannelDefinition()
            {
                Name = "name2"
            };

            channelRepository.SaveChannelDefinition(request2);

            Dictionary<string, ChannelDefinition> dict = channelRepository.GetChannelDefinitions();

            Assert.Equal("name1", dict["name1"].Name);
            Assert.Equal("name2", dict["name2"].Name);
        }
    }
}
