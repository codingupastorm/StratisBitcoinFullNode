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
        public void CanPersistAndReadBackChannelCreationRequests()
        {
            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new ChannelKeyValueStore(repositorySerializer, dataFolder, this.LoggerFactory.Object, DateTimeProvider.Default);

            var channelRepository = new ChannelRepository(this.Network, this.LoggerFactory.Object, keyValueStore, repositorySerializer);

            ChannelCreationRequest request1 = new ChannelCreationRequest()
            {
                Name = "name1",
                Organisation = "org1"
            };

            channelRepository.SaveChannelCreationRequest(request1);

            ChannelCreationRequest request2 = new ChannelCreationRequest()
            {
                Name = "name2",
                Organisation = "org2"
            };

            channelRepository.SaveChannelCreationRequest(request2);

            Dictionary<string, ChannelCreationRequest> dict = channelRepository.GetChannelCreationRequests();

            Assert.Equal("org1", dict["name1"].Organisation);
            Assert.Equal("org2", dict["name2"].Organisation);
        }
    }
}
