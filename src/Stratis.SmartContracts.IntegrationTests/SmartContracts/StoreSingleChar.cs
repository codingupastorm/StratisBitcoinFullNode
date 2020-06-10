using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class StoreSingleChar : TokenlessSmartContract
{
    public int Increment
    {
        get { return this.PersistentState.GetInt32(nameof(Increment)); }
        set { this.PersistentState.SetInt32(nameof(Increment), value); }
    }

    public StoreSingleChar(ISmartContractState state) : base(state)
    {
        this.PersistentState.SetString("Single", "1");
        this.PersistentState.SetString("Multiple", "123");
    }
}