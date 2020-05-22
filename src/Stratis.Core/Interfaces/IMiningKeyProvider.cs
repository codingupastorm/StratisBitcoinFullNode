using NBitcoin;

namespace Stratis.Core.Interfaces
{
    public interface IMiningKeyProvider
    {
        Script GetScriptPubKeyFromWallet();
    }
}
