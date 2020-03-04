using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public class TokenlessTransactionFromRWS
    {
        private const int maxScriptSizeLimit = 1_024_000;
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
            Script outputScript = TxRWSDataTemplate.Instance.GenerateScriptPubKey(opRWSData);
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            Key key = this.tokenlessWalletManager.LoadTransactionSigningKey();

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }
    }
}
