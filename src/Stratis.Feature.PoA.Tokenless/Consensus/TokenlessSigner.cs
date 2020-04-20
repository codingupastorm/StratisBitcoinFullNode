using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public class TokenlessSigner : ITokenlessSigner
    {
        public const int InputOpsLength = 8;
        public const int SenderScriptStartOpIndex = 3;

        private readonly Network network;
        private readonly ISenderRetriever senderRetriever;

        public TokenlessSigner(Network network, ISenderRetriever senderRetriever)
        {
            this.network = network;
            this.senderRetriever = senderRetriever;
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
            transaction.Inputs.Add(new TxIn(new OutPoint(dummyTx, 0)));

            // Sign the transaction in place. This will push a signature and pubkey on the start of the ScriptSig.
            TransactionBuilder builder = new TransactionBuilder(this.network)
                .AddKeys(key)
                .AddCoins(dummyTx.Outputs.AsCoins());
            builder.SignTransactionInPlace(transaction);

            // A bit of Bitcoin Script magic. Use OP_CODESEPARATOR to ensure the signature is still valid, but allow us to append to the input.	
            byte[] finalisedInputScript = transaction.Inputs[0].ScriptSig.ToBytes()
                .Concat(new byte[] { (byte)OpcodeType.OP_CODESEPARATOR })
                .Concat(senderScript.ToBytes()).ToArray();

            transaction.Inputs[0].ScriptSig = new Script(finalisedInputScript);
        }

        /// <inheritdoc />
        public GetSenderResult GetSender(Transaction transaction)
        {
            if (transaction.Inputs.Count != 1)
                return GetSenderResult.CreateFailure("Transaction must be in prescribed format: Inputs does not equal 1.");

            Script senderScript = transaction.Inputs.First().ScriptSig;
            IList<Op> ops = senderScript.ToOps();

            if (ops.Count != InputOpsLength)
                return GetSenderResult.CreateFailure("Transaction input must be in prescribed format: Invalid Ops Count.");

            Op[] senderOps = new Op[5];
            Array.Copy(ops.ToArray(), SenderScriptStartOpIndex, senderOps, 0, 5);

            return this.senderRetriever.GetAddressFromScript(new Script(senderOps));
        }

        /// <inheritdoc />
        public bool Verify(Transaction transaction)
        {
            if (transaction.Inputs.Count != 1)
                return false;

            return transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, new Script());
        }
    }
}
