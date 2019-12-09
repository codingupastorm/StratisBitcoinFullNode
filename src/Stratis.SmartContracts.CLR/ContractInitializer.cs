using System;
using System.Runtime.Serialization;
using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public class ContractInitializer<T> : IContractInitializer
    {
        public IContract CreateUninitialized(Type type, ISmartContractState state, uint160 address)
        {
            var contract = (T)FormatterServices.GetSafeUninitializedObject(type);
            return new Contract<T>(contract, type, state, address);
        }
    }
}
