using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public class MiningKeyProvider : IMiningKeyProvider
    {
        private readonly IDLTWalletManager walletManager;

        public MiningKeyProvider(IDLTWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        public Script GetScriptPubKeyFromWallet()
        {
            return this.walletManager.GetPubKey(1).ScriptPubKey;
        }
    }
}
