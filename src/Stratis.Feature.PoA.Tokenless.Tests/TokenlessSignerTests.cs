
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class TokenlessSignerTests
    {
        private readonly TokenlessSigner signer;
        private readonly Network network;

        public TokenlessSignerTests()
        {
            this.network = new TokenlessNetwork();
            this.signer = new TokenlessSigner(this.network, new SenderRetriever());
        }

        [Fact]
        public void SignTransactionThenVerifyThenGetSender()
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            var key = new Key();

            this.signer.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            Assert.Single(transaction.Inputs);

            // First 2 ops are signature and pubkey. Rest should be the code separator and then ScriptPubKey.
            IList<Op> ops = transaction.Inputs[0].ScriptSig.ToOps();
            Assert.Equal(8, ops.Count);
            Assert.Equal(OpcodeType.OP_CODESEPARATOR, ops[2].Code);
            Assert.Equal(OpcodeType.OP_DUP, ops[3].Code);
            Assert.Equal(OpcodeType.OP_CHECKSIG, ops[7].Code);

            Assert.True(transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, new Script()));
            Assert.True(this.signer.Verify(transaction));

            string expectedAddress = key.PubKey.GetAddress(this.network).ToString();
            uint160 expectedUint160 = expectedAddress.ToUint160(this.network);
            GetSenderResult getSenderResult = this.signer.GetSender(transaction);
            Assert.True(getSenderResult.Success);
            Assert.Equal(expectedUint160, getSenderResult.Sender);
        }

        [Fact]
        public void VerifyFailsWhenUsingIncorrectSender()
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            Key key = new Key();
            Key incorrectKey = new Key();

            // Sign a transaction with one key but put the sender as someone else.
            Script senderScript = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(incorrectKey.PubKey);

            Transaction dummyTx = this.network.CreateTransaction();
            dummyTx.Outputs.Add(new TxOut(Money.Zero, senderScript));

            transaction.Inputs.Add(new TxIn(new OutPoint(dummyTx, 0)));

            TransactionBuilder builder = new TransactionBuilder(this.network)
                .AddKeys(key)
                .AddCoins(dummyTx.Outputs.AsCoins());
            builder.SignTransactionInPlace(transaction);

            byte[] finalisedInputScript = transaction.Inputs[0].ScriptSig.ToBytes()
                .Concat(new byte[] { (byte)OpcodeType.OP_CODESEPARATOR })
                .Concat(senderScript.ToBytes()).ToArray();

            transaction.Inputs[0].ScriptSig = new Script(finalisedInputScript);

            Assert.False(this.signer.Verify(transaction));
        }

        [Fact]
        public void TimeIsIncludedInSignature()
        {
            var key = new Key();
            BitcoinSecret secret = key.GetBitcoinSecret(this.network);
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0, 1, 2, 3 });

            Transaction transaction1 = this.network.CreateTransaction();
            transaction1.Outputs.Add(new TxOut(Money.Zero, outputScript));

            // Transaction hash is now dependent on time, so put a bit of time between the transactions.
            Thread.Sleep(2000);

            Transaction transaction2 = this.network.CreateTransaction();
            transaction2.Outputs.Add(new TxOut(Money.Zero, outputScript));

            this.signer.InsertSignedTxIn(transaction1, secret);
            this.signer.InsertSignedTxIn(transaction2, secret);

            // We've built 2 identical transactions (except for time). We need their hashes to be different,  as hashes are used to prevent duplicates in DLT.
            Assert.NotEqual(transaction1.GetHash(), transaction2.GetHash());
        }
    }
}
