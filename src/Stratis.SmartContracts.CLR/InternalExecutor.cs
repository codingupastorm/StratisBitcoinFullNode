﻿using System.Diagnostics;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    ///<inheritdoc/>
    public sealed class InternalExecutor : IInternalTransactionExecutor
    {
        public const ulong DefaultGasLimit = GasPriceList.BaseCost * 2 - 1;

        private readonly IState state;
        private readonly IStateProcessor stateProcessor;
        private readonly IGasMeter gasMeter;
        private readonly ReadWriteSetBuilder readWriteSet;
        private readonly ReadWriteSetBuilder privateReadWriteSet;

        public InternalExecutor(IGasMeter gasMeter,
            ReadWriteSetBuilder readWriteSet,
            ReadWriteSetBuilder privateReadWriteSet,
            IState state,
            IStateProcessor stateProcessor)
        {
            this.gasMeter = gasMeter;
            this.readWriteSet = readWriteSet;
            this.privateReadWriteSet = privateReadWriteSet;
            this.state = state;
            this.stateProcessor = stateProcessor;
        }

        ///<inheritdoc />
        public ICreateResult Create<T>(ISmartContractState smartContractState,
            ulong amountToTransfer,
            object[] parameters,
            ulong gasLimit = 0)
        {
            Gas gasRemaining = this.gasMeter.GasAvailable;

            // For a method call, send all the gas unless an amount was selected.Should only call trusted methods so re - entrance is less problematic.
            ulong gasBudget = (gasLimit != 0) ? gasLimit : gasRemaining;

            Debug.WriteLine("Gas budget:" + gasBudget);

            if (gasRemaining < gasBudget || gasRemaining < GasPriceList.CreateCost)
                return CreateResult.Failed();

            var message = new InternalCreateMessage(
                smartContractState.Message.ContractAddress.ToUint160(),
                amountToTransfer,
                (Gas) gasBudget,
                parameters,
                AccountState.PolicyPlaceHolder, // TODO: Get the current contract's policy and use it to create the new policy?
                typeof(T).Name
            );

            // Create a snapshot of the current state
            IState newState = this.state.Snapshot();

            // Apply the message to the snapshot
            StateTransitionResult result = this.stateProcessor.Apply(newState, message);

            // Transition the current state to the new state
            if (result.IsSuccess)
            {
                this.state.TransitionTo(newState);
                this.readWriteSet.Merge(result.Success.ReadWriteSet);
            }

            this.gasMeter.Spend(result.GasConsumed);

            return result.IsSuccess
                ? CreateResult.Succeeded(result.Success.ContractAddress.ToAddress())
                : CreateResult.Failed();
        }

        ///<inheritdoc />
        public ITransferResult Call(
            ISmartContractState smartContractState,
            Address addressTo,
            ulong amountToTransfer,
            string methodName,
            object[] parameters,
            ulong gasLimit = 0)
        {
            Gas gasRemaining = this.gasMeter.GasAvailable;

            // For a method call, send all the gas unless an amount was selected.Should only call trusted methods so re - entrance is less problematic.
            ulong gasBudget = (gasLimit != 0) ? gasLimit : gasRemaining;

            if (gasRemaining < gasBudget || gasRemaining < GasPriceList.BaseCost)
                return TransferResult.Failed();

            var message = new InternalCallMessage(
                addressTo.ToUint160(),
                smartContractState.Message.ContractAddress.ToUint160(),
                amountToTransfer,
                (Gas) gasBudget,
                new MethodCall(methodName, parameters)
            );

            // Create a snapshot of the current state
            IState newState = this.state.Snapshot();

            // Apply the message to the snapshot
            StateTransitionResult result = this.stateProcessor.Apply(newState, message);

            // Transition the current state to the new state
            if (result.IsSuccess)
            {
                this.state.TransitionTo(newState);
                this.readWriteSet.Merge(result.Success.ReadWriteSet);
                this.privateReadWriteSet.Merge(result.Success.PrivateReadWriteSet);
            }

            this.gasMeter.Spend(result.GasConsumed);

            return result.IsSuccess
                ? TransferResult.Transferred(result.Success.ExecutionResult)
                : TransferResult.Failed();
        }

        ///<inheritdoc />
        public ITransferResult Transfer(ISmartContractState smartContractState, Address addressTo, ulong amountToTransfer)
        {
            Gas gasRemaining = this.gasMeter.GasAvailable;

            if (gasRemaining < GasPriceList.TransferCost)
                return TransferResult.Failed();

            ulong gasBudget = (gasRemaining < DefaultGasLimit) 
                ? gasRemaining // have enough for at least a transfer but not for the DefaultGasLimit
                : DefaultGasLimit; // have enough for anything

            var message = new ContractTransferMessage(
                addressTo.ToUint160(),
                smartContractState.Message.ContractAddress.ToUint160(),
                amountToTransfer,
                (Gas) gasBudget
            );

            // Create a snapshot of the current state
            IState newState = this.state.Snapshot();

            // Apply the message to the snapshot
            StateTransitionResult result = this.stateProcessor.Apply(newState, message);

            // Transition the current state to the new state
            if (result.IsSuccess)
            {
                this.state.TransitionTo(newState);
                this.readWriteSet.Merge(result.Success.ReadWriteSet);
            }

            this.gasMeter.Spend(result.GasConsumed);

            return result.IsSuccess
                ? TransferResult.Empty()
                : TransferResult.Failed();
        }
    }
}