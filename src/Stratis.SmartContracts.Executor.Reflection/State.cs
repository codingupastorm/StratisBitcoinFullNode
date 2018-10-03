using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection.ContractLogging;
using Stratis.SmartContracts.Executor.Reflection.Serialization;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents the current state of the world during a contract execution.
    /// <para>
    /// The state contains several components:
    /// </para>
    /// - The state repository, which contains global account, code, and contract data.
    /// - Internal transfers, which are transfers generated internally by contracts.
    /// - Balance state, which represents the intermediate state of the balances based on the internal transfers list.
    /// - The log holder, which contains logs generated by contracts during execution.
    /// <para>
    /// When a message is applied to the state, the state is updated if the application was successful. Otherwise, the state
    /// is rolled back to a previous snapshot. This works equally for nested state transitions generated by internal creates,
    /// calls and transfers.
    /// </para>
    /// </summary>
    public class State : IState
    {
        private readonly List<TransferInfo> internalTransfers;

        private IState child;
        private readonly ISmartContractStateFactory smartContractStateFactory;

        private State(State state)
        {
            this.ContractState = state.ContractState.StartTracking();
            
            // We create a new log holder but use references to the original raw logs
            this.LogHolder = new ContractLogHolder(state.Network);
            this.LogHolder.AddRawLogs(state.LogHolder.GetRawLogs());

            // We create a new list but use references to the original transfers.
            this.internalTransfers = new List<TransferInfo>(state.InternalTransfers);

            // Create a new balance state based off the old one but with the repository and internal transfers list reference
            this.BalanceState = new BalanceState(this.ContractState, state.BalanceState.TxAmount, this.internalTransfers);
            this.Network = state.Network;
            this.NonceGenerator = state.NonceGenerator;
            this.Block = state.Block;
            this.TransactionHash = state.TransactionHash;
            this.smartContractStateFactory = state.smartContractStateFactory;
        }

        public State(
            ISmartContractStateFactory smartContractStateFactory, 
            IStateRepository repository,
            IContractLogHolder contractLogHolder,
            List<TransferInfo> internalTransfers,
            IBlock block,
            Network network,
            ulong txAmount,
            uint256 transactionHash)
        {
            this.ContractState = repository;
            this.LogHolder = contractLogHolder;
            this.internalTransfers = internalTransfers;
            this.BalanceState = new BalanceState(this.ContractState, txAmount, this.InternalTransfers);
            this.Network = network;
            this.NonceGenerator = new NonceGenerator();
            this.Block = block;
            this.TransactionHash = transactionHash;
            this.smartContractStateFactory = smartContractStateFactory;
        }
        
        public uint256 TransactionHash { get; }

        public IBlock Block { get; }

        private Network Network { get; }

        public NonceGenerator NonceGenerator { get; }

        public IContractLogHolder LogHolder { get; }

        public BalanceState BalanceState { get; }

        public IReadOnlyList<TransferInfo> InternalTransfers => this.internalTransfers;

        public IStateRepository ContractState { get; }

        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>
         public ISmartContractState CreateSmartContractState(IState state, GasMeter gasMeter, uint160 address, BaseMessage message, IStateRepository repository) 
        {
            return this.smartContractStateFactory.Create(state, gasMeter, address, message, repository);
        }

        /// <summary>
        /// Returns contract logs in the log type used by consensus.
        /// </summary>
        public IList<Log> GetLogs(IContractPrimitiveSerializer serializer)
        {
            return this.LogHolder.GetRawLogs().ToLogs(serializer);
        }       

        public void TransitionTo(IState state)
        {
            if (this.child != state)
            {
                throw new ArgumentException("New state must be a child of this state.");
            }

            // Update internal transfers
            this.internalTransfers.Clear();
            this.internalTransfers.AddRange(state.InternalTransfers);

            // Update logs
            this.LogHolder.Clear();
            this.LogHolder.AddRawLogs(state.LogHolder.GetRawLogs());

            // Commit the state to update the parent state
            state.ContractState.Commit();

            this.child = null;
        }

        public void AddInternalTransfer(TransferInfo transferInfo)
        {
            this.internalTransfers.Add(transferInfo);
        }

        public void InsertInternalTransfer(int index, TransferInfo transferInfo)
        {
            this.internalTransfers.Insert(index, transferInfo);
        }

        public ulong GetBalance(uint160 address)
        {
            return this.BalanceState.GetBalance(address);
        }

        public uint160 GenerateAddress(IAddressGenerator addressGenerator)
        {
            return addressGenerator.GenerateAddress(this.TransactionHash, this.NonceGenerator.Next);
        }

        /// <summary>
        /// Returns a mutable snapshot of the current state. Changes can be made to the snapshot, then discarded or applied to the parent state.
        /// To update this state with changes made to the snapshot, call <see cref="TransitionTo"/>. Only one valid snapshot can exist. If a new
        /// snapshot is created, the parent state will reject any transitions from older snapshots.
        /// </summary>
        public IState Snapshot()
        {
            this.child = new State(this);

            return this.child;
        }
    }
}