using Stratis.SmartContracts;

namespace Stratis.HyperContracts
{
    public interface IHyperContractState
    {
        // TODO: Custom messages etc.
        IPersistentState PersistentState { get; }
    }
}
