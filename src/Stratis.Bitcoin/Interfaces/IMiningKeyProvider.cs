using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IMiningKeyProvider
    {
        Script GetScriptPubKeyFromWallet();
    }
}
