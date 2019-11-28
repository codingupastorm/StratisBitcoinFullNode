using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    public interface IWritableStateDb
    {
        void SetState(uint160 contractAddress, string key, StateValue data);

        void SetContractState(uint160 contractAddress, ContractState contractState);

        void SetContractCode(uint160 contractAddress, byte[] code);

        void SetContractType(uint160 contractAddress, string typeName);
    }
}
