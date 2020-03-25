using Stratis.SmartContracts;
using Stratis.SmartContracts.Tokenless;

public class PrivateDataContract : TokenlessSmartContract
{
    public PrivateDataContract(ISmartContractState state) : base(state)
    {
    }


    public void StoreTransientData()
    {
        // Store the transient bytes in the public store for everyone. This is proof that transient data is being passed in correctly.
        this.PersistentState.SetBytes("Transient", this.TransientData);

        // Now store the transient data in the private store. This is proof that we can store things in the private store.
        this.PrivateState.SetBytes("TransientPrivate", this.TransientData);
    }

}