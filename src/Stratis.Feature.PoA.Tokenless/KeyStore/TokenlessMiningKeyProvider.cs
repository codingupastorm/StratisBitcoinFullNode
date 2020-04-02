using NBitcoin;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.KeyStore
{
    public class TokenlessMiningKeyProvider : IMiningKeyProvider
    {
        /// <summary>
        /// Returns an empty script to be used when building blocks.
        /// <para>
        /// It passes the null check in PoAMiner, so no error message is shown
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
