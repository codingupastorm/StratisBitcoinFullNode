using NBitcoin;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IMiningKeyProvider
    {
        Script GetScriptPubKeyFromWallet();
    }
}
