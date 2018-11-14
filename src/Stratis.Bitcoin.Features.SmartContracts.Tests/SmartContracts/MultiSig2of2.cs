using Stratis.SmartContracts;

public class MultiSig2of2 : SmartContract
{
    public Address Owner1
    {
        get
        {
            return PersistentState.GetAddress("Owner1");
        }
        set
        {
            PersistentState.SetAddress("Owner1", value);
        }
    }

    public Address Owner2
    {
        get
        {
            return PersistentState.GetAddress("Owner2");
        }
        set
        {
            PersistentState.SetAddress("Owner2", value);
        }
    }

    public uint TransactionCount
    {
        get
        {
            return PersistentState.GetUInt32("TxCount");
        }
        set
        {
            PersistentState.SetUInt32("TxCount", value);
        }
    }

    public Transaction GetTransaction(uint transactionId)
    {
        return PersistentState.GetStruct<Transaction>($"Transactions:{transactionId}");
    }

    private void SetTransaction(uint transactionId, Transaction transaction)
    {
        PersistentState.SetStruct<Transaction>($"Transactions:{transactionId}", transaction);
    }

    public MultiSig2of2(ISmartContractState smartContractState, Address owner1, Address owner2)
        : base(smartContractState)
    {
        Owner1 = owner1;
        Owner2 = owner2;
    }

    public void Deposit()
    {
        Assert(Message.Sender == Owner1 || Message.Sender == Owner2);
    }

    public void SubmitTransaction(Address to, ulong amount)
    {
        Assert(Message.Sender == Owner1 || Message.Sender == Owner2);
        SetTransaction(TransactionCount, new Transaction{Amount = amount, Executed = false, Initiator = Message.Sender, To = to});
        TransactionCount++;
    }

    public void ApproveTransaction(uint transactionId)
    {
        Assert(Message.Sender == Owner1 || Message.Sender == Owner2);
        Transaction toExecute = GetTransaction(transactionId);
        Assert(!toExecute.Executed);
        Assert(toExecute.Initiator != Message.Sender);
        Assert(Transfer(toExecute.To, toExecute.Amount).Success);
        toExecute.Executed = true;
        SetTransaction(transactionId, toExecute);
    }

    public struct Transaction
    {
        public Address To;
        public ulong Amount;
        public Address Initiator;
        public bool Executed;
    }
}