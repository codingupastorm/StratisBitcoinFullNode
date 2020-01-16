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

            ExtKey seedExtKey = TokenlessWallet.GetSeedExtKey(mnemonic);

            int coinType = 500;

            Key privateKey = TokenlessWallet.GetExtKey(coinType, seedExtKey, TokenlessWalletAccount.BlockSigning, 0).PrivateKey;

            ExtPubKey account = TokenlessWallet.GetAccountExtPubKey(coinType, seedExtKey, TokenlessWalletAccount.BlockSigning);

            Assert.Equal(privateKey.PubKey, TokenlessWallet.GetPubKey(account, 0));
        }
    }
}
