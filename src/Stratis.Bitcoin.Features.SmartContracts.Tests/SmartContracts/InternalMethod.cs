using Stratis.SmartContracts;

public class InternalMethod : SmartContract
{
    public InternalMethod(ISmartContractState state) : base(state)
    {
    }

    internal int CallInternalMethod()
    {
        return 21;
    }
}