using System;
using Stratis.SmartContracts;

public class Splitter : SmartContract
{
    public Splitter(ISmartContractState state) : base(state)
    {
    }

    public void Split(Address address1, Address address2)
    {
        Assert(Transfer(address1, Message.Value / 2).Success);
        Assert(Transfer(address2, Message.Value / 2).Success);
    }
}
