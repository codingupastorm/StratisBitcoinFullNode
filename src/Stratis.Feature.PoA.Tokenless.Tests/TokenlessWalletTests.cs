using System.Threading.Tasks;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class TokenlessWalletTests
    {
        [Fact]
        public void GetPubKeyMatchesPublicKeyFromGetExtKey()
        {
            var mnemonic = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom");

            ExtKey seedExtKey = TokenlessKeyStore.GetSeedExtKey(mnemonic);

            int coinType = 500;

            Key privateKey = TokenlessKeyStore.GetKey(coinType, seedExtKey, TokenlessWalletAccount.BlockSigning, 0);

            ExtPubKey account = TokenlessKeyStore.GetAccountExtPubKey(coinType, seedExtKey, TokenlessWalletAccount.BlockSigning);

            Assert.Equal(privateKey.PubKey, TokenlessKeyStore.GetPubKey(account, 0));
        }
    }
}
