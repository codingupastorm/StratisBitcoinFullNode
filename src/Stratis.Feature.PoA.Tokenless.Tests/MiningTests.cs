using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.Feature.PoA.Tokenless.Mining;
using Stratis.Patricia;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tokenless;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Tokenless
{
    public sealed class MiningTests
    {
        private readonly CachedCoinView cachedCoinView;
        private readonly MempoolSettings mempoolSettings;
        private readonly IEnumerable<IMempoolRule> mempoolRules;
        private readonly InMemoryCoinView inMemoryCoinView;
        private readonly ChainIndexer chainIndexer;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILoggerFactory loggerFactory;
        private readonly Network network;
        private readonly NodeSettings nodeSettings;
        private ConsensusSettings consensusSettings;
        private StateRepositoryRoot stateRoot;
        private SmartContractValidator validator;
        private AddressGenerator AddressGenerator;
        private ContractAssemblyLoader<TokenlessSmartContract> assemblyLoader;
        private CallDataSerializer callDataSerializer;
        private ContractModuleDefinitionReader moduleDefinitionReader;
        private ContractAssemblyCache contractCache;
        private ReflectionVirtualMachine reflectionVirtualMachine;
        private StateProcessor stateProcessor;
        private InternalExecutorFactory internalTxExecutorFactory;
        private ContractPrimitiveSerializer primitiveSerializer;
        private Serializer serializer;
        private SmartContractStateFactory smartContractStateFactory;
        private StateFactory stateFactory;
        private BasicKeyEncodingStrategy keyEncodingStrategy;
        private TokenlessSigner tokenlessSigner;
        private string folder;

        private TokenlessReflectionExecutorFactory executorFactory;

        private BlockExecutionResultCache executionCache;
        private ChainState chainState;
        private ConsensusManager consensusManager;
        private TokenlessMempool mempool;
        private MempoolSchedulerLock mempoolLock;
        private ConsensusRuleEngine consensusRules;

        public MiningTests()
        {
            this.network = new TokenlessNetwork();
            this.chainIndexer = new ChainIndexer(this.network);
            this.nodeSettings = new NodeSettings(this.network);
            this.consensusSettings = new ConsensusSettings(this.nodeSettings);

            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.inMemoryCoinView = new InMemoryCoinView(this.chainIndexer.Tip.HashBlock);

            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.dateTimeProvider = DateTimeProvider.Default;
            this.cachedCoinView = new CachedCoinView(this.inMemoryCoinView, this.dateTimeProvider, this.loggerFactory, new NodeStats(this.dateTimeProvider, this.loggerFactory), this.consensusSettings);

            this.mempoolSettings = new MempoolSettings(this.nodeSettings) { MempoolExpiry = MempoolValidator.DefaultMempoolExpiry };
            this.mempoolRules = CreateMempoolRules();
        }

        [Fact]
        public async Task Build_Tokenless_BlockDefinition_With_SmartContractBytecode_Async()
        {
            await InitializeAsync();
            var mempoolValidator = CreateTokenlessMempoolValidator();
            var blockDefinition = CreateBlockDefinition();

            // Create a smart contract transaction
            var transaction = this.network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessExample.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();
            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            var mempoolValidationState = new MempoolValidationState(false);
            await mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transaction);
            Assert.Single(this.mempool.MapTx);

            var block = blockDefinition.Build(this.chainIndexer.Tip, null);
            Assert.Single(block.Block.Transactions);

            var result = this.executionCache.GetExecutionResult(block.Block.GetHash());
            Assert.NotNull(result);
            Assert.Single(result.Receipts);
            Assert.True(result.Receipts.First().Success);

            uint160 contractAddress = this.AddressGenerator.GenerateAddress(transaction.GetHash(), 0);
            Assert.NotNull(result.MutatedStateRepository.GetCode(contractAddress));


            byte[] senderValue = result.MutatedStateRepository.GetStorageValue(contractAddress, Encoding.UTF8.GetBytes("Sender"));
            byte[] expectedSenderValue = key.PubKey.GetAddress(this.network).ToString().ToUint160(this.network).ToBytes();
            Assert.Equal(expectedSenderValue, senderValue);
        }

        [Fact]
        public async Task Build_Tokenless_BlockDefinition_WithOut_SmartContractBytecode_Async()
        {
            await InitializeAsync();

            var mempoolValidator = CreateTokenlessMempoolValidator();
            var transaction = this.network.CreateTransaction();

            var mempoolValidationState = new MempoolValidationState(false);
            await mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transaction);
            Assert.Single(this.mempool.MapTx);

            var blockDefinition = CreateBlockDefinition();
            var block = blockDefinition.Build(this.chainIndexer.Tip, null);
            Assert.Single(block.Block.Transactions);
        }

        private TokenlessMempoolValidator CreateTokenlessMempoolValidator()
        {
            return new TokenlessMempoolValidator(
                this.chainIndexer,
                this.cachedCoinView,
                this.dateTimeProvider,
                this.loggerFactory,
                this.mempool,
                new MempoolSchedulerLock(),
                this.mempoolRules,
                this.mempoolSettings);
        }

        private BlockDefinition CreateBlockDefinition()
        {
            return new TokenlessBlockDefinition(
                new BlockBufferGenerator(),
                this.cachedCoinView,
                this.consensusManager,
                this.dateTimeProvider,
                this.executorFactory,
                new ExtendedLoggerFactory(),
                this.mempool,
                this.mempoolLock,
                new MinerSettings(this.nodeSettings),
                this.network,
                this.tokenlessSigner,
                this.stateRoot,
                this.executionCache,
                this.callDataSerializer);
        }

        private async Task InitializeAsync()
        {
            this.chainState = new ChainState()
            {
                BlockStoreTip = new ChainedHeader(this.network.GetGenesis().Header, this.network.GetGenesis().GetHash(), 0)
            };

            InitializeConsensusRules();

            this.consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.network, chainState: this.chainState, inMemoryCoinView: this.inMemoryCoinView, chainIndexer: this.chainIndexer, consensusRules: this.consensusRules);
            await this.consensusManager.InitializeAsync(this.chainIndexer.Tip);
            this.mempool = new TokenlessMempool(new BlockPolicyEstimator(new MempoolSettings(this.nodeSettings), this.loggerFactory, this.nodeSettings), this.loggerFactory, this.nodeSettings);
            this.mempoolLock = new MempoolSchedulerLock();

            InitializeSmartContractComponents();
        }

        private void InitializeConsensusRules()
        {
            var consensusRulesContainer = new ConsensusRulesContainer();

            //consensusRulesContainer.HeaderValidationRules.Add(Activator.CreateInstance(typeof(BitcoinHeaderVersionRule)) as HeaderValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new SetActivationDeploymentsFullValidationRule() as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new LoadCoinviewRule() as FullValidationConsensusRule);

            //consensusRulesContainer.FullValidationRules.Add(new TxOutSmartContractExecRule() as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new OpSpendRule() as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new CanGetSenderRule(senderRetriever) as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new P2PKHNotContractRule(this.StateRoot) as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new CanGetSenderRule(senderRetriever) as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new SmartContractPowCoinviewRule(this.network, this.StateRoot, this.ExecutorFactory, this.callDataSerializer, senderRetriever, receiptRepository, this.cachedCoinView, this.executionCache, new LoggerFactory()) as FullValidationConsensusRule);
            //consensusRulesContainer.FullValidationRules.Add(new SaveCoinviewRule() as FullValidationConsensusRule);

            this.consensusRules = new PowConsensusRuleEngine(
                    this.network,
                    this.loggerFactory,
                    DateTimeProvider.Default,
                    this.chainIndexer,
                    new NodeDeployments(this.network, this.chainIndexer),
                    this.consensusSettings,
                    new Checkpoints(),
                    this.cachedCoinView,
                    this.chainState,
                    new InvalidBlockHashStore(this.dateTimeProvider),
                    new NodeStats(this.dateTimeProvider, this.loggerFactory),
                    new AsyncProvider(this.loggerFactory, new Signals.Signals(this.loggerFactory, null), new NodeLifetime()),
                    consensusRulesContainer)
                .SetupRulesEngineParent();
        }

        private void InitializeSmartContractComponents([CallerMemberName] string callingMethod = "")
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

            this.folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestCase", callingMethod));
            var engine = new ContractStateTableStore(Path.Combine(this.folder, "contracts"), this.loggerFactory, this.dateTimeProvider, new DBreezeSerializer(this.network.Consensus.ConsensusFactory));
            var byteStore = new DBreezeByteStore(engine, "ContractState1");
            byteStore.Empty();
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);

            this.stateRoot = new StateRepositoryRoot(stateDB);
            this.validator = new SmartContractValidator();

            this.AddressGenerator = new AddressGenerator();

            this.assemblyLoader = new ContractAssemblyLoader<TokenlessSmartContract>();
            var contractInitializer = new ContractInitializer<TokenlessSmartContract>();
            this.callDataSerializer = new NoGasCallDataSerializer(new ContractPrimitiveSerializer(this.network));
            this.moduleDefinitionReader = new ContractModuleDefinitionReader();
            this.contractCache = new ContractAssemblyCache();

            this.reflectionVirtualMachine = new ReflectionVirtualMachine(this.validator, this.loggerFactory, this.assemblyLoader, this.moduleDefinitionReader, this.contractCache, contractInitializer);
            this.stateProcessor = new StateProcessor(this.reflectionVirtualMachine, this.AddressGenerator);
            this.internalTxExecutorFactory = new InternalExecutorFactory(this.loggerFactory, this.stateProcessor);
            this.primitiveSerializer = new ContractPrimitiveSerializer(this.network);
            this.serializer = new Serializer(this.primitiveSerializer);
            this.smartContractStateFactory = new SmartContractStateFactory(this.primitiveSerializer, this.internalTxExecutorFactory, this.serializer);
            this.stateFactory = new StateFactory(this.smartContractStateFactory);
            this.executorFactory = new TokenlessReflectionExecutorFactory(this.callDataSerializer, this.stateFactory, this.stateProcessor, this.primitiveSerializer);

            this.executionCache = new BlockExecutionResultCache();

            this.tokenlessSigner = new TokenlessSigner(this.network, new SenderRetriever());
        }

        private IEnumerable<IMempoolRule> CreateMempoolRules()
        {
            foreach (var ruleType in this.network.Consensus.MempoolRules)
            {
                if (ruleType == typeof(IsSmartContractWellFormedMempoolRule))
                    yield return (IMempoolRule)Activator.CreateInstance(ruleType, this.network, this.mempool, this.mempoolSettings, this.chainIndexer, this.loggerFactory, this.callDataSerializer);
                else
                    yield return (IMempoolRule)Activator.CreateInstance(ruleType, this.network, this.mempool, this.mempoolSettings, this.chainIndexer, this.loggerFactory);
            }
        }
    }
}
