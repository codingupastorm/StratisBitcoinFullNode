using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class TokenlessPureExample : TokenlessSmartContract
{

    public TokenlessPureExample(ISmartContractState state) : base(state)
    {
        for (int i = 0; i < 5; i++)
        {
            this.PersistentState.SetInt32($"Mapping[{i}]", i*i);
        }
    }

    [Pure]
    public int GetItemAtIndex(int index)
    {
        return this.PersistentState.GetInt32($"Mapping[{index}]");
    }

}