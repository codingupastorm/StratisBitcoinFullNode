using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class PrivateDataContract : TokenlessSmartContract
{
    public PrivateDataContract(ISmartContractState state) : base(state)
    {
    }


    public void StoreTransientData()
    {
        this.PrivateState.SetBytes("Transient", this.TransientData);
    }

}