using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public interface IReadWriteSetTransactionSerializer 
    {
        Transaction Build(ReadWriteSet readWriteSet);
        ReadWriteSet GetReadWriteSet(Transaction tx);
    }

    public class ReadWriteSetTransactionSerializer : IReadWriteSetTransactionSerializer
    {
        private readonly Network network;
        private readonly IEndorsementSigner endorsementSigner;

        public ReadWriteSetTransactionSerializer(Network network, IEndorsementSigner endorsementSigner)
        {
            this.network = network;
            this.endorsementSigner = endorsementSigner;
        }

        public Transaction Build(ReadWriteSet readWriteSet)
        {
            string json = readWriteSet.ToJson();
            byte[] opRWSData = Encoding.UTF8.GetBytes(json);
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxReadWriteDataTemplate.Instance.GenerateScriptPubKey(opRWSData);
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            this.endorsementSigner.Sign(transaction);

            return transaction;
        }

        public ReadWriteSet GetReadWriteSet(Transaction tx)
        {
            if (tx.Outputs.Count < 1)
                return null;

            var rwsData = TxReadWriteDataTemplate.Instance.ExtractScriptPubKeyParameters(tx.Outputs[0].ScriptPubKey);
            if (rwsData == null || rwsData.Length != 1)
                return null;

            string json = Encoding.UTF8.GetString(rwsData[0]);
            return new ReadWriteSet().FromJson(json);
        }
    }
}
