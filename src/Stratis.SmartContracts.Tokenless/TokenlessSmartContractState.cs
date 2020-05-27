﻿using System;

namespace Stratis.SmartContracts.Tokenless
{
    /// <summary>
    /// Smart contract state that gets injected into the smart contract by the <see cref="ReflectionVirtualMachine"/>.
    /// </summary>
    public sealed class TokenlessSmartContractState : ISmartContractState
    {
        public TokenlessSmartContractState(
            IBlock block,
            IMessage message,
            IPersistentState persistentState,
            IPersistentState privateState,
            ISerializer serializer,
            IContractLogger contractLogger,
            IInternalTransactionExecutor internalTransactionExecutor,
            IInternalHashHelper internalHashHelper,
            Func<ulong> getBalance,
            byte[] transientData)
        {
            this.Block = block;
            this.Message = message;
            this.PersistentState = persistentState;
            this.PrivateState = privateState;
            this.Serializer = serializer;
            this.ContractLogger = contractLogger;
            this.InternalTransactionExecutor = internalTransactionExecutor;
            this.InternalHashHelper = internalHashHelper;
            this.GetBalance = getBalance;
            this.TransientData = transientData;
        }

        public IBlock Block { get; }

        public IMessage Message { get; }

        public IPersistentState PersistentState { get; }

        public IPersistentState PrivateState { get; }

        public ISerializer Serializer { get; }

        public IContractLogger ContractLogger { get; }

        public IInternalTransactionExecutor InternalTransactionExecutor { get; }

        public IInternalHashHelper InternalHashHelper { get; }

        public Func<ulong> GetBalance { get; }

        public byte[] TransientData { get; }
    }
}