using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Base;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public sealed class TipsManagerTests : TestBase
    {
        private KeyValueRepository keyValueRepo;
        private readonly LoggerFactory loggerFactory;
        private readonly List<ChainedHeader> mainChainHeaders;
        private TipsManager tipsManager;

        public TipsManagerTests() : base(new StratisMain())
        {
            this.loggerFactory = new LoggerFactory();
            this.mainChainHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(20, ChainedHeadersHelper.CreateGenesisChainedHeader(this.Network), true);
        }

        private void InitializeTipsManager(string testFolder)
        {
            var repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new KeyValueRepositoryStore(repositorySerializer, new DataFolder(testFolder), this.loggerFactory, DateTimeProvider.Default);
            this.keyValueRepo = new KeyValueRepository(keyValueStore, repositorySerializer);

            this.tipsManager = new TipsManager(this.keyValueRepo, this.loggerFactory);
        }

        [Fact]
        public void InitializesAtGenesis()
        {
            InitializeTipsManager(CreateTestDir(this));

            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            ChainedHeader commonTip = this.tipsManager.GetLastCommonTip();

            Assert.Equal(this.Network.GenesisHash, commonTip.HashBlock);
        }

        [Fact]
        public async Task InitializesAtLastSavedValueAsync()
        {
            InitializeTipsManager(CreateTestDir(this));

            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var tipProvider = new TestTipProvider();
            this.tipsManager.RegisterTipProvider(tipProvider);
            this.tipsManager.CommitTipPersisted(tipProvider, this.mainChainHeaders[10]);
            Assert.Equal(this.mainChainHeaders[10], this.tipsManager.GetLastCommonTip());

            // Give it some time to save tip in bg.
            await Task.Delay(500);

            this.tipsManager.Dispose();

            var newTipsManager = new TipsManager(this.keyValueRepo, this.loggerFactory);
            newTipsManager.Initialize(this.mainChainHeaders.Last());

            Assert.Equal(this.mainChainHeaders[10], newTipsManager.GetLastCommonTip());
        }

        [Fact]
        public void CommonTipCalculatedCorrectlyWhenProvidersAreOnTheSameChain()
        {
            InitializeTipsManager(CreateTestDir(this));

            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var provider1 = new TestTipProvider();
            var provider2 = new TestTipProvider();
            var provider3 = new TestTipProvider();

            this.tipsManager.RegisterTipProvider(provider1);
            this.tipsManager.RegisterTipProvider(provider2);
            this.tipsManager.RegisterTipProvider(provider3);

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[10]);
            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[9]);

            // genesis is common because only 2\3 providers commited anything.
            Assert.Equal(this.mainChainHeaders[0], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[5]);

            // 3rd provider is lowest, therefore it's tip is the common.
            Assert.Equal(this.mainChainHeaders[5], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[2]);

            // First provider rewinded before everyone else. Now it's tip is the lowest and common.
            Assert.Equal(this.mainChainHeaders[2], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[15]);

            // Nothing changes after rest of providers advance.
            Assert.Equal(this.mainChainHeaders[2], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[14]);

            Assert.Equal(this.mainChainHeaders[14], this.tipsManager.GetLastCommonTip());
        }

        [Fact]
        public void CommonTipCalculatedCorrectlyWhenProvidersAreOnDifferentChains()
        {
            InitializeTipsManager(CreateTestDir(this));

            // Chain that forks at block 12
            List<ChainedHeader> altChainHeaders = ChainedHeadersHelper.CreateConsecutiveHeaders(5, this.mainChainHeaders[12]);

            this.tipsManager.Initialize(this.mainChainHeaders.Last());

            var provider1 = new TestTipProvider();
            var provider2 = new TestTipProvider();
            var provider3 = new TestTipProvider();
            this.tipsManager.RegisterTipProvider(provider1);
            this.tipsManager.RegisterTipProvider(provider2);
            this.tipsManager.RegisterTipProvider(provider3);

            this.tipsManager.CommitTipPersisted(provider1, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider2, this.mainChainHeaders[15]);
            this.tipsManager.CommitTipPersisted(provider3, altChainHeaders[4]);

            Assert.Equal(this.mainChainHeaders[12], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider3, this.mainChainHeaders[18]);
            Assert.Equal(this.mainChainHeaders[15], this.tipsManager.GetLastCommonTip());

            this.tipsManager.CommitTipPersisted(provider1, altChainHeaders[2]);
            this.tipsManager.CommitTipPersisted(provider2, altChainHeaders[3]);
            this.tipsManager.CommitTipPersisted(provider3, altChainHeaders[4]);

            Assert.Equal(altChainHeaders[2], this.tipsManager.GetLastCommonTip());
        }

        private class TestTipProvider : ITipProvider
        {
        }
    }
}
