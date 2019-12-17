using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class TokenlessSimpleContract : TokenlessSmartContract
{
    public int Increment
    {
        get { return this.PersistentState.GetInt32(nameof(Increment)); }
        set { this.PersistentState.SetInt32(nameof(Increment), value); }
    }

    public TokenlessSimpleContract(ISmartContractState state) : base(state)
    {
    }


    public int CallMe()
    {
        this.Increment = this.Increment + 1;
        return this. Increment;
    }

}