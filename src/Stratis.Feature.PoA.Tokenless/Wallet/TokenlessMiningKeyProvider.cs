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
        /// <para>
        /// It also passes the null check in PoAMiner, so no error message is shown
        /// about not having rewards.
        /// </para>
        /// </summary>
        /// <returns>Returns an empty script.</returns>
        public Script GetScriptPubKeyFromWallet()
        {
            return new Script();
        }
    }
}
