﻿using NBitcoin;

namespace Stratis.Features.ContractEndorsement.State
{
    public interface IReadableContractStateDb
    {
        bool IsExist(uint160 addr);

        ContractState GetContractState(uint160 addr);

        byte[] GetCode(uint160 addr);

        byte[] GetCodeHash(uint160 addr);

        string GetContractType(uint160 addr);

        StateValue GetState(uint160 contractAddress, string key);
    }
}
