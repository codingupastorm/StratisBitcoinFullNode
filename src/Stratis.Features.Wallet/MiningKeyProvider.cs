using System.Linq;
using NBitcoin;
using Stratis.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.Wallet
{
    public class MiningKeyProvider : IMiningKeyProvider
    {
        private readonly IWalletManager walletManager;

        public MiningKeyProvider(IWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        /// <summary>Gets scriptPubKey from the wallet.</summary>
        public Script GetScriptPubKeyFromWallet()
        {
            string walletName = this.walletManager.GetWalletsNames().FirstOrDefault();

            if (walletName == null)
                return null;

            HdAccount account = this.walletManager.GetAccounts(walletName).FirstOrDefault();

            if (account == null)
                return null;

            var walletAccountReference = new WalletAccountReference(walletName, account.Name);

            HdAddress address = this.walletManager.GetUnusedAddress(walletAccountReference);

            return address.Pubkey;
        }
    }
}
