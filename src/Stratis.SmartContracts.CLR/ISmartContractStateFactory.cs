using NBitcoin;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface ISmartContractStateFactory
    {
        /// <summary>
        /// Sets up a new <see cref="ISmartContractState"/> based on the current state.
        /// </summary>
        /// <param name="version">The version that will be stored with any writes.</param>
        ISmartContractState Create(IState state,
            IGasMeter gasMeter,
            uint160 address,
            BaseMessage message,
            IStateRepository repository);
    }
}