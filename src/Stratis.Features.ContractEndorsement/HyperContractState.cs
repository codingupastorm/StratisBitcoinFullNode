using Stratis.HyperContracts;
using Stratis.SmartContracts;

namespace Stratis.Features.ContractEndorsement
{
    public class HyperContractState : IHyperContractState
    {
        public IPersistentState PersistentState { get; }
    }
}
