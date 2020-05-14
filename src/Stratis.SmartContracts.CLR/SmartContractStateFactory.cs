using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.CLR
{
    public class SmartContractStateFactory : ISmartContractStateFactory
    {
        private readonly ISerializer serializer;
        private readonly IContractPrimitiveSerializer primitiveSerializer;
        private readonly IInternalExecutorFactory internalTransactionExecutorFactory;
        private readonly IPrivateDataStore privateDataStore;

        public SmartContractStateFactory(IContractPrimitiveSerializer primitiveSerializer,
            IInternalExecutorFactory internalTransactionExecutorFactory,
            IPrivateDataStore privateDataStore,
            ISerializer serializer)
        {
            this.serializer = serializer;
            this.primitiveSerializer = primitiveSerializer;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
            this.privateDataStore = privateDataStore;
        }

        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>
        public ISmartContractState Create(
            IState state,
            ReadWriteSetBuilder readWriteSet,
            ReadWriteSetBuilder privateReadWriteSet,
            IGasMeter gasMeter,
            uint160 address,
            BaseMessage message,
            IStateRepository repository)
        {
            IPersistenceStrategy persistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy(), readWriteSet, state.Version);

            var privatePersistenceStrategy = new PrivateMeteredPersistenceStrategy(this.privateDataStore, gasMeter, new BasicKeyEncodingStrategy(), new PrivateReadWriteSetOperations(readWriteSet, privateReadWriteSet), state.Version);

            var persistentState = new PersistentState(persistenceStrategy, this.serializer, address);

            var privateState = new PrivatePersistentState(this.serializer, privatePersistenceStrategy, address);

            var contractLogger = new MeteredContractLogger(gasMeter, state.LogHolder, this.primitiveSerializer);

            var contractState = new TokenlessSmartContractState(
                state.Block,
                new Message(
                    address.ToAddress(),
                    message.From.ToAddress(),
                    message.Amount
                ),
                persistentState,
                privateState,
                this.serializer,
                contractLogger,
                this.internalTransactionExecutorFactory.Create(gasMeter, readWriteSet, privateReadWriteSet, state),
                new InternalHashHelper(),
                () => state.GetBalance(address), 
                state.TransientData);

            return contractState;
        }
    }
}