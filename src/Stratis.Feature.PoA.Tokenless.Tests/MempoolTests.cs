using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Feature.PoA.Tokenless.Tests;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Tokenless
{
    public sealed class MempoolTests
    {
        private readonly TokenlessTestHelper helper;

        public MempoolTests()
        {
            this.helper = new TokenlessTestHelper();
        }

        [Fact]
        public async Task SubmitToTokenlessMempool_Accepted_Async()
        {
            Transaction transaction = this.helper.Network.CreateTransaction();

            var key = new Key();
            this.helper.TokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.helper.Network));

            var mempoolValidationState = new MempoolValidationState(false);
            await this.helper.MempoolValidator.AcceptToMemoryPool(mempoolValidationState, transaction);

            Assert.Equal(1, this.helper.Mempool.Size);
        }

        [Fact]
        public void SubmitToTokenlessMempool_Failed_Async()
        {
            // TODO-TL: These need to be completed.
        }
    }
}
