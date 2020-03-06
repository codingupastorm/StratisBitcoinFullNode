using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public interface ITokenlessTransactionFromRWS 
    {
        Transaction Build(ReadWriteSet readWriteSet);
        ReadWriteSet GetReadWriteSet(Transaction tx);
    }

    public class TokenlessTransactionFromRWS : ITokenlessTransactionFromRWS
    {
        private readonly Network network;
        private readonly ITokenlessWalletManager tokenlessWalletManager;
        private readonly ITokenlessSigner tokenlessSigner;

        public TokenlessTransactionFromRWS(Network network, ITokenlessWalletManager tokenlessWalletManager, ITokenlessSigner tokenlessSigner)
        {
            this.network = network;
            this.tokenlessWalletManager = tokenlessWalletManager;
            this.tokenlessSigner = tokenlessSigner;
        }

        public Transaction Build(ReadWriteSet readWriteSet)
        {
            string json = readWriteSet.ToJson();
            byte[] opRWSData = Encoding.UTF8.GetBytes(json);
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxReadWriteDataTemplate.Instance.GenerateScriptPubKey(opRWSData);
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            Key key = this.tokenlessWalletManager.LoadTransactionSigningKey();

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

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
