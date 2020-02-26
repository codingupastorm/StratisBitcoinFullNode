using System;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Smart contract state that gets injected into the smart contract by the <see cref="ReflectionVirtualMachine"/>.
    /// </summary>
    public sealed class SmartContractState : ISmartContractState
    {
        public SmartContractState(
            IBlock block,
            IMessage message,
            IPersistentState persistentState,
            ISerializer serializer,
            IContractLogger contractLogger,
            IInternalTransactionExecutor internalTransactionExecutor,
            IInternalHashHelper internalHashHelper)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.Serializer = serializer;
            this.ContractLogger = contractLogger;
            this.InternalTransactionExecutor = internalTransactionExecutor;
            this.InternalHashHelper = internalHashHelper;
        }

        public IBlock Block { get; }

        public IMessage Message { get; }

        public IPersistentState PersistentState { get; }

        public ISerializer Serializer { get; }
        public Func<ulong> GetBalance => throw new InvalidOperationException("Shouldn't be touched in Tokenless.");

        public IContractLogger ContractLogger { get; }

        public IInternalTransactionExecutor InternalTransactionExecutor { get; }

        public IInternalHashHelper InternalHashHelper { get; }
    }
}