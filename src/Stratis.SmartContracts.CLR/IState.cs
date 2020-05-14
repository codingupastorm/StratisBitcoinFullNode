﻿using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface IState
    {
        string Version { get; }
        IBlock Block { get; }
        BalanceState BalanceState { get; }
        IStateRepository ContractState { get; }
        IList<Log> GetLogs(IContractPrimitiveSerializer serializer);
        IReadOnlyList<TransferInfo> InternalTransfers { get; }
        IContractLogHolder LogHolder { get; }
        IState Snapshot();
        NonceGenerator NonceGenerator { get; }
        void TransitionTo(IState state);
        void AddInternalTransfer(TransferInfo transferInfo);
        ulong GetBalance(uint160 address);
        uint160 GenerateAddress(IAddressGenerator addressGenerator);
        ISmartContractState CreateSmartContractState(IState state, ReadWriteSetBuilder readWriteSet, ReadWriteSetBuilder privateReadWriteSet, IGasMeter gasMeter, uint160 address, BaseMessage message, IStateRepository repository);
        void AddInitialTransfer(TransferInfo initialTransfer);

        /// <summary>
        /// Data that will optionally be sent with a private data transaction.
        /// </summary>
        byte[] TransientData { get; }
    }
}