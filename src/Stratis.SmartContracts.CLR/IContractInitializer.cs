using System;
using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public interface IContractInitializer
    {
        IContract CreateUninitialized(Type type, ISmartContractState state, uint160 address);
    }
}
