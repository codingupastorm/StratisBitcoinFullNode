using System.Linq;
using DBreeze.Utils;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public class InputCreator
    {
        private readonly Network network;

        public InputCreator(Network network)
        {
            this.network = network;
        }

        public void InsertSenderTxInAndSign(Transaction transaction, uint160 senderAddress, ISecret key)
        {
            // Script to ourselves
            Script senderScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(new KeyId(senderAddress));

            // Make a txout we are pretending to spend.
            Transaction dummyTx = this.network.CreateTransaction();
            dummyTx.Outputs.Add(new TxOut(Money.Zero, senderScript));

            transaction.Inputs.Add(new TxIn(new OutPoint(dummyTx, 0)));
            transaction.Inputs[0].ScriptSig = senderScript;
            TransactionBuilder builder = new TransactionBuilder(this.network)
                .AddKeys(key)
                .AddCoins(dummyTx.Outputs.AsCoins());

            builder.SignTransactionInPlace(transaction);

            bool isLegit = transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, senderScript);

            transaction.Inputs[0].ScriptSig = new Script(transaction.Inputs[0].ScriptSig.ToBytes().Concat((byte) OpcodeType.OP_CODESEPARATOR).Concat(senderScript.ToBytes()));

            isLegit = transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, new Script());
        }
    }
}
