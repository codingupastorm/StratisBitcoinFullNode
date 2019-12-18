using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class TokenlessExample : TokenlessSmartContract
{
    public TokenlessExample(ISmartContractState state) : base(state)
    {
        this.PersistentState.SetAddress("Sender", Message.Sender);
    }
}