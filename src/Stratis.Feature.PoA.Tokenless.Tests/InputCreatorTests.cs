
using System.Linq;
using NBitcoin;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class InputCreatorTests
    {
        private readonly InputHelper inputCreator;
        private readonly Network network;

        public InputCreatorTests()
        {
            this.network = new TokenlessNetwork();
            this.inputCreator = new InputHelper(this.network);
        }

        [Fact]
        public void AddInputForOpReturnOutput()
        {
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] {0, 1, 2, 3});
            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            var key = new Key();

            this.inputCreator.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            Assert.Single(transaction.Inputs);
            Assert.True(transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, new Script()));

            //string address = key.PubKey.GetAddress(this.network).ToString();
            //uint160 addressUint160 = address.ToUint160(this.network);
        }
    }
}
