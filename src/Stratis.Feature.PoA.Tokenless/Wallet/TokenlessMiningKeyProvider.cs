using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public class TokenlessMiningKeyProvider : IMiningKeyProvider
    {
        private readonly ITokenlessWalletManager walletManager;

        public TokenlessMiningKeyProvider(ITokenlessWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Returning null ensures that an empty script is used to build blocks.
        /// </summary>
        /// <returns>Returns a <c>null</c> script.</returns>
        public Script GetScriptPubKeyFromWallet()
        {
            return null;
        }
    }
}
