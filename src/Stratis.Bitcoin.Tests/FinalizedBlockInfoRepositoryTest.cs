﻿using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.AsyncWork;
using Stratis.Core.Configuration;
using Stratis.Core.Consensus;
using Stratis.Core.Networks;
using Stratis.Core.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class FinalizedBlockInfoRepositoryTest : TestBase
    {
        private readonly ILoggerFactory loggerFactory;

        public FinalizedBlockInfoRepositoryTest() : base(new StratisRegTest())
        {
            this.loggerFactory = new LoggerFactory();
        }

        [Fact]
        public async Task FinalizedHeightSavedOnDiskAsync()
        {
            string dir = CreateTestDir(this);
            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new KeyValueRepositoryStore(repositorySerializer, new DataFolder(dir), this.loggerFactory, DateTimeProvider.Default);
            var kvRepo = new KeyValueRepository(keyValueStore, repositorySerializer);
            var asyncMock = new Mock<IAsyncProvider>();
            asyncMock.Setup(a => a.RegisterTask(It.IsAny<string>(), It.IsAny<Task>()));

            using (var repo = new FinalizedBlockInfoRepository(kvRepo, this.loggerFactory, asyncMock.Object))
            {
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 777);
            }

            using (var repo = new FinalizedBlockInfoRepository(kvRepo, this.loggerFactory, asyncMock.Object))
            {
                await repo.LoadFinalizedBlockInfoAsync(this.Network);
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }

        [Fact]
        public async Task FinalizedHeightCantBeDecreasedAsync()
        {
            string dir = CreateTestDir(this);
            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new KeyValueRepositoryStore(repositorySerializer, new DataFolder(dir), this.loggerFactory, DateTimeProvider.Default);
            var kvRepo = new KeyValueRepository(keyValueStore, repositorySerializer);
            var asyncMock = new Mock<IAsyncProvider>();
            asyncMock.Setup(a => a.RegisterTask(It.IsAny<string>(), It.IsAny<Task>()));

            using (var repo = new FinalizedBlockInfoRepository(kvRepo, this.loggerFactory, asyncMock.Object))
            {
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 777);
                repo.SaveFinalizedBlockHashAndHeight(uint256.One, 555);

                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }

            using (var repo = new FinalizedBlockInfoRepository(kvRepo, this.loggerFactory, asyncMock.Object))
            {
                await repo.LoadFinalizedBlockInfoAsync(this.Network);
                Assert.Equal(777, repo.GetFinalizedBlockInfo().Height);
            }
        }
    }
}
