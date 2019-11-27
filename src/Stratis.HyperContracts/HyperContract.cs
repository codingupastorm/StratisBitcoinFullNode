using Stratis.SmartContracts;

namespace Stratis.HyperContracts
{
    public abstract class HyperContract
    {
        // TODO: Probably don't consume Stratis.SmartContracts. Drop everything in here instead.

        protected IPersistentState PersistentState { get; }

        public HyperContract(IHyperContractState state)
        {
            this.PersistentState = state.PersistentState;
        }
    }
}
