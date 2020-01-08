using System;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public sealed class TokenlessWalletException : Exception
    {
        public TokenlessWalletException(string message) : base(message)
        {
        }
    }
}
