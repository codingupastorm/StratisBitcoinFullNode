using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Feature.PoA.Tokenless
{
    public class InputHelper : ITokenlessInputHelper
    {
        private readonly Network network;

        public InputHelper(Network network)
        {
            this.network = network;
        }

        /// <inheritdoc />
        public void InsertSignedTxIn(Transaction transaction, ISecret key)
        {
            // We can only add a Sender TxIn to a transaction without any current inputs.
            Guard.Assert(transaction.Inputs.IsEmpty());

            // Generate a normal ScriptPubKey to Sender.
            Script senderScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PrivateKey.PubKey);

            // Imagine there was a TxOut sent to us. Required to get the TransactionBuilder to sign tokenless transactions.
            Transaction dummyTx = this.network.CreateTransaction();
            dummyTx.Outputs.Add(new TxOut(Money.Zero, senderScript));

            // Add an input to the user's transaction. Referencing the fake TxOut and with the ScriptPubKey.
            var input = new TxIn(new OutPoint(dummyTx, 0));
            input.ScriptSig = senderScript;
            transaction.Inputs.Add(input);

            // Sign the transaction in place. This will push a signature and pubkey on the start of the ScriptSig.
            TransactionBuilder builder = new TransactionBuilder(this.network)
                .AddKeys(key)
                .AddCoins(dummyTx.Outputs.AsCoins());
            builder.SignTransactionInPlace(transaction);
        }
    }
}
