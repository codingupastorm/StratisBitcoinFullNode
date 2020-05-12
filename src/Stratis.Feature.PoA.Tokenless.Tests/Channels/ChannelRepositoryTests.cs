using System.Collections.Generic;
using System.Text.Json;
using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Networks;
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
            ChannelNetwork salesChannelNetwork = SystemChannelNetwork.CreateChannelNetwork("sales", "salesfolder", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            salesChannelNetwork.Id = 2;
            salesChannelNetwork.AccessList = new AccessControlList
            {
                Organisations = new List<string>
                {
                    "Sales"
                }
            };
            salesChannelNetwork.DefaultAPIPort = 1;
            salesChannelNetwork.DefaultPort = 2;
            salesChannelNetwork.DefaultSignalRPort = 3;
            var salesNetworkJson = JsonSerializer.Serialize(salesChannelNetwork);

            ChannelNetwork marketingChannelNetwork = SystemChannelNetwork.CreateChannelNetwork("marketing", "marketingfolder", DateTimeProvider.Default.GetAdjustedTimeAsUnixTimestamp());
            marketingChannelNetwork.Id = 3;
            marketingChannelNetwork.AccessList = new AccessControlList
            {
                Organisations = new List<string>
                {
                    "Marketing"
                }
            };
            marketingChannelNetwork.DefaultAPIPort = 4;
            marketingChannelNetwork.DefaultPort = 5;
            marketingChannelNetwork.DefaultSignalRPort = 6;
            var marketingNetworkJson = JsonSerializer.Serialize(marketingChannelNetwork);

            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new ChannelKeyValueStore(repositorySerializer, dataFolder, this.LoggerFactory.Object);

            var channelRepository = new ChannelRepository(this.LoggerFactory.Object, keyValueStore);
            channelRepository.Initialize();

            var salesChannel = new ChannelDefinition()
            {
                Id = channelRepository.GetNextChannelId(),
                Name = "sales",
                AccessListJson = salesChannelNetwork.AccessList.ToJson(),
                NetworkJson = salesNetworkJson
            };
            channelRepository.SaveChannelDefinition(salesChannel);

            var marketingChannel = new ChannelDefinition()
            {
                Id = channelRepository.GetNextChannelId(),
                Name = "marketing",
                AccessListJson = marketingChannelNetwork.AccessList.ToJson(),
                NetworkJson = marketingNetworkJson
            };

            channelRepository.SaveChannelDefinition(marketingChannel);

            Dictionary<string, ChannelDefinition> channels = channelRepository.GetChannelDefinitions();

            Assert.Equal(2, channels["sales"].Id);
            Assert.Equal("sales", channels["sales"].Name);
            Assert.Contains("Sales", channels["sales"].AccessListJson);
            Assert.Equal(salesNetworkJson, channels["sales"].NetworkJson);

            Assert.Equal(3, channels["marketing"].Id);
            Assert.Equal("marketing", channels["marketing"].Name);
            Assert.Contains("Marketing", channels["marketing"].AccessListJson);
            Assert.Equal(marketingNetworkJson, channels["marketing"].NetworkJson);

            ChannelDefinition salesChannelDefinition = channelRepository.GetChannelDefinition("sales");

            Assert.Equal(2, salesChannelDefinition.Id);
            Assert.Equal("sales", salesChannelDefinition.Name);
            Assert.Contains("Sales", salesChannelDefinition.AccessListJson);
            Assert.Equal(salesNetworkJson, salesChannelDefinition.NetworkJson);
        }

        [Fact]
        public void CanPersistAndReadBackChannelMembers()
        {
            var dataFolderPath = CreateTestDir(this);
            var dataFolder = new DataFolder(dataFolderPath);

            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new ChannelKeyValueStore(repositorySerializer, dataFolder, this.LoggerFactory.Object);

            var channelRepository = new ChannelRepository(this.LoggerFactory.Object, keyValueStore);

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
