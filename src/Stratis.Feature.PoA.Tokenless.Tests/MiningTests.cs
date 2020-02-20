﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Caching;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Mempool;
using Stratis.Feature.PoA.Tokenless.Mining;
using Stratis.Patricia;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tokenless;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class MiningTests
    {
        private readonly TokenlessTestHelper helper;

        private readonly CachedCoinView cachedCoinView;
        private readonly ConsensusSettings consensusSettings;

        private StateRepositoryRoot stateRoot;
        private SmartContractValidator validator;
        private AddressGenerator AddressGenerator;
        private ContractAssemblyLoader<TokenlessSmartContract> assemblyLoader;
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
        private string folder;

        private TokenlessReflectionExecutorFactory executorFactory;

        private BlockExecutionResultCache executionCache;
        private ChainState chainState;
        private ConsensusManager consensusManager;
        private MempoolSchedulerLock mempoolLock;
        private ConsensusRuleEngine consensusRules;

        public MiningTests()
        {
            this.helper = new TokenlessTestHelper();
            this.consensusSettings = new ConsensusSettings(this.helper.NodeSettings);
            this.cachedCoinView = new CachedCoinView(this.helper.InMemoryCoinView, this.helper.DateTimeProvider, this.helper.LoggerFactory, new NodeStats(this.helper.DateTimeProvider, this.helper.LoggerFactory), this.consensusSettings);
        }

        [Fact]
        public async Task Build_Tokenless_BlockDefinition_With_SmartContractBytecode_Async()
        {
            await InitializeAsync();

            TokenlessMempoolValidator mempoolValidator = CreateTokenlessMempoolValidator();
            BlockDefinition blockDefinition = CreateBlockDefinition();

            // Create a smart contract transaction
            Transaction transaction = this.helper.Network.CreateTransaction();
            ContractCompilationResult compilationResult = ContractCompiler.CompileFile("SmartContracts/TokenlessExample.cs");
            Assert.True(compilationResult.Success);

            var contractTxData = new ContractTxData(0, 0, (Gas)0, compilationResult.Compilation);
            byte[] outputScript = this.helper.CallDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            var key = new Key();
            this.helper.TokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.helper.Network));

            var mempoolValidationState = new MempoolValidationState(false);
            await mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transaction);
            Assert.Single(this.helper.Mempool.MapTx);

            BlockTemplate block = blockDefinition.Build(this.helper.ChainIndexer.Tip, null);
            Assert.Equal(2, block.Block.Transactions.Count);

            BlockExecutionResultModel result = this.executionCache.GetExecutionResult(block.Block.GetHash());
            Assert.NotNull(result);
            Assert.Single(result.Receipts);
            Assert.True(result.Receipts.First().Success);

            uint160 contractAddress = this.AddressGenerator.GenerateAddress(transaction.GetHash(), 0);
            Assert.NotNull(result.MutatedStateRepository.GetCode(contractAddress));

            byte[] senderValue = result.MutatedStateRepository.GetStorageValue(contractAddress, Encoding.UTF8.GetBytes("Sender")).Value;
            byte[] expectedSenderValue = key.PubKey.GetAddress(this.helper.Network).ToString().ToUint160(this.helper.Network).ToBytes();
            Assert.Equal(expectedSenderValue, senderValue);
        }

        [Fact]
        public async Task Build_Tokenless_BlockDefinition_WithOut_SmartContractBytecode_Async()
        {
            await InitializeAsync();

            var mempoolValidationState = new MempoolValidationState(false);
            TokenlessMempoolValidator mempoolValidator = CreateTokenlessMempoolValidator();
            var key = new Key();

            // Transaction One
            Transaction transactionOne = this.helper.Network.CreateTransaction();
            this.helper.TokenlessSigner.InsertSignedTxIn(transactionOne, key.GetBitcoinSecret(this.helper.Network));
            await mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transactionOne);
            TestBase.WaitLoop(() => { return this.helper.Mempool.MapTx.Count == 1; });

            await Task.Delay(1000); // Small delay so that the transaction has different time.

            // Transaction Two
            Transaction transactionTwo = this.helper.Network.CreateTransaction();
            this.helper.TokenlessSigner.InsertSignedTxIn(transactionTwo, key.GetBitcoinSecret(this.helper.Network));
            await mempoolValidator.AcceptToMemoryPool(mempoolValidationState, transactionTwo);
            TestBase.WaitLoop(() => { return this.helper.Mempool.MapTx.Count == 2; });

            BlockDefinition blockDefinition = CreateBlockDefinition();
            BlockTemplate block = blockDefinition.Build(this.helper.ChainIndexer.Tip, null);
            Assert.Equal(3, block.Block.Transactions.Count);

            // Ensure that the transaction one was added before transaction two.
            Assert.True(block.Block.Transactions[1].Time < block.Block.Transactions[2].Time);
        }

        private TokenlessMempoolValidator CreateTokenlessMempoolValidator()
        {
            return new TokenlessMempoolValidator(
                this.helper.ChainIndexer,
                this.cachedCoinView,
                this.helper.DateTimeProvider,
                this.helper.LoggerFactory,
                this.helper.Mempool,
                this.mempoolLock,
                this.helper.MempoolRules,
                this.helper.MempoolSettings);
        }

        private BlockDefinition CreateBlockDefinition()
        {
            return new TokenlessBlockDefinition(
                new BlockBufferGenerator(),
                this.cachedCoinView,
                this.consensusManager,
                this.helper.DateTimeProvider,
                this.executorFactory,
                new ExtendedLoggerFactory(),
                this.helper.Mempool,
                this.mempoolLock,
                new MinerSettings(this.helper.NodeSettings),
                this.helper.Network,
                this.helper.TokenlessSigner,
                this.stateRoot,
                this.executionCache,
                this.helper.CallDataSerializer);
        }

        private async Task InitializeAsync()
        {
            this.chainState = new ChainState()
            {
                BlockStoreTip = new ChainedHeader(this.helper.Network.GetGenesis().Header, this.helper.Network.GetGenesis().GetHash(), 0)
            };

            InitializeConsensusRules();

            this.consensusManager = ConsensusManagerHelper.CreateConsensusManager(this.helper.Network, chainState: this.chainState, inMemoryCoinView: this.helper.InMemoryCoinView, chainIndexer: this.helper.ChainIndexer, consensusRules: this.consensusRules);
            await this.consensusManager.InitializeAsync(this.helper.ChainIndexer.Tip);
            this.mempoolLock = new MempoolSchedulerLock();

            InitializeSmartContractComponents();
        }

        private void InitializeConsensusRules()
        {
            var consensusRulesContainer = new ConsensusRulesContainer();

            this.consensusRules = new PowConsensusRuleEngine(
                    this.helper.Network,
                    this.helper.LoggerFactory,
                    DateTimeProvider.Default,
                    this.helper.ChainIndexer,
                    new NodeDeployments(this.helper.Network, this.helper.ChainIndexer),
                    this.consensusSettings,
                    new Checkpoints(),
                    this.cachedCoinView,
                    this.chainState,
                    new InvalidBlockHashStore(this.helper.DateTimeProvider),
                    new NodeStats(this.helper.DateTimeProvider, this.helper.LoggerFactory),
                    new AsyncProvider(this.helper.LoggerFactory, new Signals(this.helper.LoggerFactory, null), new NodeLifetime()),
                    consensusRulesContainer)
                .SetupRulesEngineParent();
        }

        private void InitializeSmartContractComponents([CallerMemberName] string callingMethod = "")
        {
            this.keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

            this.folder = TestBase.AssureEmptyDir(Path.Combine(AppContext.BaseDirectory, "TestCase", callingMethod));
            var engine = new ContractStateTableStore(Path.Combine(this.folder, "contracts"), this.helper.LoggerFactory, this.helper.DateTimeProvider, new RepositorySerializer(this.helper.Network.Consensus.ConsensusFactory));
            var byteStore = new KeyValueByteStore(engine, "ContractState1");
            byteStore.Empty();
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(byteStore);

            this.stateRoot = new StateRepositoryRoot(stateDB);
            this.validator = new SmartContractValidator();

            this.AddressGenerator = new AddressGenerator();

            this.assemblyLoader = new ContractAssemblyLoader<TokenlessSmartContract>();
            var contractInitializer = new ContractInitializer<TokenlessSmartContract>();
            this.moduleDefinitionReader = new ContractModuleDefinitionReader();
            this.contractCache = new ContractAssemblyCache();

            this.reflectionVirtualMachine = new ReflectionVirtualMachine(this.validator, this.helper.LoggerFactory, this.assemblyLoader, this.moduleDefinitionReader, this.contractCache, contractInitializer);
            this.stateProcessor = new StateProcessor(this.reflectionVirtualMachine, this.AddressGenerator);
            this.internalTxExecutorFactory = new InternalExecutorFactory(this.helper.LoggerFactory, this.stateProcessor);
            this.primitiveSerializer = new ContractPrimitiveSerializer(this.helper.Network);
            this.serializer = new Serializer(this.primitiveSerializer);
            this.smartContractStateFactory = new SmartContractStateFactory(this.primitiveSerializer, this.internalTxExecutorFactory, this.serializer);
            this.stateFactory = new StateFactory(this.smartContractStateFactory);
            this.executorFactory = new TokenlessReflectionExecutorFactory(this.helper.CallDataSerializer, this.stateFactory, this.stateProcessor, this.primitiveSerializer);

            this.executionCache = new BlockExecutionResultCache();
        }
    }
}