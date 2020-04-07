using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.CLR
{
    public class SmartContractStateFactory : ISmartContractStateFactory
    {
        private readonly ISerializer serializer;
        private readonly IContractPrimitiveSerializer primitiveSerializer;
        private readonly IInternalExecutorFactory internalTransactionExecutorFactory;

        public SmartContractStateFactory(IContractPrimitiveSerializer primitiveSerializer,
            IInternalExecutorFactory internalTransactionExecutorFactory,
            ISerializer serializer)
        {
            this.serializer = serializer;
            this.primitiveSerializer = primitiveSerializer;
            this.internalTransactionExecutorFactory = internalTransactionExecutorFactory;
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

            var privatePersistenceStrategy = new MeteredPersistenceStrategy(repository, gasMeter, new BasicKeyEncodingStrategy(), new PrivateReadWriteSetOperations(readWriteSet, privateReadWriteSet), state.Version);

            var persistentState = new PersistentState(persistenceStrategy, this.serializer, address);

            var privateState = new PrivatePersistentState(this.serializer, privatePersistenceStrategy, address, privateReadWriteSet);

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