using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.CLR.ContractLogging;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public interface IState
    {
        IBlock Block { get; }
        IStateRepository ContractState { get; }
        IList<Log> GetLogs(IContractPrimitiveSerializer serializer);
        IContractLogHolder LogHolder { get; }
        IState Snapshot();
        NonceGenerator NonceGenerator { get; }
        void TransitionTo(IState state);
        uint160 GenerateAddress(IAddressGenerator addressGenerator);
        ISmartContractState CreateSmartContractState(IState state, RuntimeObserver.IGasMeter gasMeter, uint160 address, BaseMessage message, IStateRepository repository);
    }
}