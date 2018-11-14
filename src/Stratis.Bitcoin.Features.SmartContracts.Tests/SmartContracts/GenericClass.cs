using Stratis.SmartContracts;

public class GenericClass<T> : SmartContract
{
    public GenericClass(ISmartContractState state) : base(state)
    {
    }

    public T Test()
    {
        return default(T);
    }
}
