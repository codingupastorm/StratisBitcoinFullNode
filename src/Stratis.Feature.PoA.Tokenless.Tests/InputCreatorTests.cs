
using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class InputCreatorTests
    {
        private readonly InputCreator inputCreator;
        private readonly Network network;

        public InputCreatorTests()
        {
            this.network = new TokenlessNetwork();
            this.inputCreator = new InputCreator(this.network);
        }

        [Fact]
        public void AddInputForOpReturnOutput()
        {
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(new byte[] {0, 1, 2, 3});
            Transaction transaction = this.network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            var key = new Key();
            string address = key.PubKey.GetAddress(this.network).ToString();
            uint160 addressUint160 = address.ToUint160(this.network);

            this.inputCreator.InsertSenderTxInAndSign(transaction, addressUint160, key.GetBitcoinSecret(this.network));

            Assert.Single(transaction.Inputs);

            bool isLegit = transaction.Inputs.AsIndexedInputs().First().VerifyScript(this.network, new Script());

        }
    }
}
