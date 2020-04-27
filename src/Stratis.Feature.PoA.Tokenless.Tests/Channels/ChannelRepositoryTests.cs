using System.Collections.Generic;
using NBitcoin;
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

            var channelRepository = new ChannelRepository(this.LoggerFactory.Object, keyValueStore);
            channelRepository.Initialize();

            var request1 = new ChannelDefinition()
            {
                Id = channelRepository.GetNextChannelId(),
                Name = "name1"
            };
            channelRepository.SaveChannelDefinition(request1);

            var request2 = new ChannelDefinition()
            {
                Id = channelRepository.GetNextChannelId(),
                Name = "name2"
            }; channelRepository.SaveChannelDefinition(request2);

            Dictionary<string, ChannelDefinition> dict = channelRepository.GetChannelDefinitions();

            Assert.Equal("name1", dict["name1"].Name);
            Assert.Equal("name2", dict["name2"].Name);
            Assert.Equal(2, dict["name1"].Id);
            Assert.Equal(3, dict["name2"].Id);
        }

        [Fact]
        public void CanPersistAndReadBackChannelMembers()
        {
            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new ChannelKeyValueStore(repositorySerializer, dataFolder, this.LoggerFactory.Object, DateTimeProvider.Default);

            var channelRepository = new ChannelRepository(this.Network, this.LoggerFactory.Object, keyValueStore, repositorySerializer);

            channelRepository.Initialize();

            var member1 = new Key().PubKey.ToString();
            var request1 = new ChannelMemberDefinition()
            {
                ChannelName = "name1",
                MemberPublicKey = member1
            };

            channelRepository.SaveMemberDefinition(request1);

            var member2 = new Key().PubKey.ToString();
            var request2 = new ChannelMemberDefinition()
            {
                ChannelName = "name1",
                MemberPublicKey = member2
            };

            channelRepository.SaveMemberDefinition(request2);

            Dictionary<string, ChannelMemberDefinition> dict = channelRepository.GetMemberDefinitions("name1");

            Assert.Equal(member1, dict[member1].MemberPublicKey);
            Assert.Equal("name1", dict[member1].ChannelName);
            Assert.Equal(member2, dict[member2].MemberPublicKey);
            Assert.Equal("name1", dict[member2].ChannelName);
        }
    }
}
