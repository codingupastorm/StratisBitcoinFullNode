using System;
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

        private Script GenerateScriptPubKey(params byte[][] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            var ops = new Op[data.Length + 1];
            ops[0] = OpcodeType.OP_RWS;
            for (int i = 0; i < data.Length; i++)
            {
                ops[1 + i] = Op.GetPushOp(data[i]);
            }
            var script = new Script(ops);
            if (script.ToBytes(true).Length > maxScriptSizeLimit)
                throw new ArgumentOutOfRangeException("data", "Data in OP_RWS should have a maximum size of " + maxScriptSizeLimit + " bytes");
            return script;
        }

        public Transaction Build(ReadWriteSet readWriteSet)
        {
            string json = readWriteSet.ToJson();
            byte[] opRWSData = Encoding.UTF8.GetBytes(json);
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = this.GenerateScriptPubKey(opRWSData);
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            Key key = this.tokenlessWalletManager.LoadTransactionSigningKey();

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return transaction;
        }
    }
}
