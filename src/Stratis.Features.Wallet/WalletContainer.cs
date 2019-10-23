using Stratis.Features.Wallet.Tables;

namespace Stratis.Features.Wallet
{
    public class WalletContainer
    {
        // TODO: Immutability

        public WalletDatabase Database { get; set; }

        public WalletDto Wallet { get; set; }
    }
}
