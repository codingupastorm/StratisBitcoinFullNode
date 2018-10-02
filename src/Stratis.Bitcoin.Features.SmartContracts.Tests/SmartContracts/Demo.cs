﻿using Stratis.SmartContracts;

public class Demo : SmartContract
{
    public Demo(ISmartContractState state) : base(state)
    {
        Counter = 12345;
        TestSave = "Hello, smart contract world!";
    }

    public int Counter
    {
        get
        {
            return PersistentState.GetAsInt32("Counter");
        }
        set
        {
            PersistentState.SetInt32("Counter", value);
        }
    }

    public string TestSave
    {
        get
        {
            return PersistentState.GetAsString("TestSave");
        }
        set
        {
            PersistentState.SetString("TestSave", value);
        }
    }

    public void Increment()
    {
        Counter++;
    }
}
