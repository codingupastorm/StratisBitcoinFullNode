using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    public interface IWritableStateDb
    {
        void SetStorageValue(uint160 contractAddress, string key, StorageValue data);

        void SetContractState(uint160 contractAddress, ContractState contractState);

        void SetContractCode(uint160 contractAddress, byte[] code);

        void SetContractType(uint160 contractAddress, string typeName);
    }
}
