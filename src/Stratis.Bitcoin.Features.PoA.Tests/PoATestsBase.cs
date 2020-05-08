using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.PoA;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.AsyncWork;
using Stratis.Core.Utilities;
using Stratis.Features.PoA.Voting;

namespace Stratis.Features.PoA.Tests
{
    public class PoATestsBase
    {
        protected readonly ChainedHeader currentHeader;
        protected readonly TestPoANetwork2 network;
        protected readonly PoAConsensusOptions consensusOptions;

        protected PoAConsensusRuleEngine rulesEngine;
        protected readonly LoggerFactory loggerFactory;
        protected readonly PoABlockHeaderValidator poaHeaderValidator;
        protected readonly ISlotsManager slotsManager;
        protected readonly ConsensusSettings consensusSettings;
        protected readonly ChainIndexer ChainIndexer;
        protected readonly IFederationManager federationManager;
        protected readonly VotingManager votingManager;
        protected readonly Mock<IPollResultExecutor> resultExecutorMock;
        protected readonly Mock<ChainIndexer> chainIndexerMock;
        protected readonly ISignals signals;
        protected readonly RepositorySerializer repositorySerializer;
        protected readonly ChainState chainState;
        protected readonly IAsyncProvider asyncProvider;

        public PoATestsBase(TestPoANetwork2 network = null)
        {
            this.loggerFactory = new LoggerFactory();
            this.signals = new Signals(this.loggerFactory, null);
            this.network = network ?? new TestPoANetwork2();
            this.consensusOptions = this.network.ConsensusOptions;
            this.repositorySerializer = new RepositorySerializer(this.network.Consensus.ConsensusFactory);

            this.ChainIndexer = new ChainIndexer(this.network);
            IDateTimeProvider timeProvider = new DateTimeProvider();
            this.consensusSettings = new ConsensusSettings(NodeSettings.Default(this.network));

            this.federationManager = CreateFederationManager(this, this.network, this.loggerFactory, this.signals);

            this.chainIndexerMock = new Mock<ChainIndexer>();
            this.chainIndexerMock.Setup(x => x.Tip).Returns(new ChainedHeader(new BlockHeader(), 0, 0));
            this.slotsManager = new SlotsManager(this.network, this.federationManager, this.chainIndexerMock.Object);

            this.poaHeaderValidator = new PoABlockHeaderValidator(this.loggerFactory);
            this.asyncProvider = new AsyncProvider(this.loggerFactory, this.signals, new Mock<INodeLifetime>().Object);

            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            var keyValueStore = new KeyValueRepositoryStore(this.repositorySerializer, dataFolder, this.loggerFactory, DateTimeProvider.Default);
            var finalizedBlockRepo = new FinalizedBlockInfoRepository(new KeyValueRepository(keyValueStore, this.repositorySerializer), this.loggerFactory, this.asyncProvider);
            finalizedBlockRepo.LoadFinalizedBlockInfoAsync(this.network).GetAwaiter().GetResult();

            this.resultExecutorMock = new Mock<IPollResultExecutor>();

            var pollsKeyValueStore = new PollsKeyValueStore(this.repositorySerializer, dataFolder, this.loggerFactory, timeProvider);

            this.votingManager = new VotingManager(this.federationManager, this.loggerFactory, this.slotsManager, this.resultExecutorMock.Object, new NodeStats(timeProvider, this.loggerFactory),
                 this.signals, finalizedBlockRepo, pollsKeyValueStore);

            this.votingManager.Initialize();

            this.chainState = new ChainState();


            this.rulesEngine = new PoAConsensusRuleEngine(this.network, this.loggerFactory, new DateTimeProvider(), this.ChainIndexer, new NodeDeployments(this.network, this.ChainIndexer),
                this.consensusSettings, new Checkpoints(this.network, this.consensusSettings), new Mock<ICoinView>().Object, this.chainState, new InvalidBlockHashStore(timeProvider),
                new NodeStats(timeProvider, this.loggerFactory), this.slotsManager, this.poaHeaderValidator, this.votingManager, this.federationManager, this.asyncProvider, new ConsensusRulesContainer());

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(50, null, false, null, this.network);

            this.currentHeader = headers.Last();
        }

        public static IFederationManager CreateFederationManager(object caller, Network network, LoggerFactory loggerFactory, ISignals signals)
        {
            string dir = TestBase.CreateTestDir(caller);
            var repositorySerializer = new RepositorySerializer(network.Consensus.ConsensusFactory);
            var keyValueStore = new KeyValueRepositoryStore(repositorySerializer, new DataFolder(dir), loggerFactory, DateTimeProvider.Default);
            var keyValueRepo = new KeyValueRepository(keyValueStore, repositorySerializer);

            var settings = new NodeSettings(network, args: new string[] { $"-datadir={dir}" });
            var federationManager = new FederationManager(settings, network, loggerFactory, keyValueRepo, signals);
            federationManager.Initialize();

            return federationManager;
        }

        public static IFederationManager CreateFederationManager(object caller)
        {
            return CreateFederationManager(caller, new TestPoANetwork2(), new ExtendedLoggerFactory(), new Signals(new LoggerFactory(), null));
        }

        public void InitRule(ConsensusRuleBase rule)
        {
            rule.Parent = this.rulesEngine;
            rule.Logger = this.loggerFactory.CreateLogger(rule.GetType().FullName);
            rule.Initialize();
        }
    }
}
