using System;
using Stratis.SmartContracts;

public class LogTest : SmartContract
{
    public LogTest(ISmartContractState state) : base(state)
    {
    }

    public void StoreLog(string message)
    {
        Log(new ReceivedAtBlock{Amount = Message.Value, Sender = Message.Sender, Message = message});
    }

    public struct ReceivedAtBlock
    {
        [Index]
        public Address Sender;

        [Index]
        public ulong Amount;

        public string Message;
    }
}
