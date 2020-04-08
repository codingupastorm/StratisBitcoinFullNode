using System;

namespace Stratis.Feature.PoA.Tokenless.KeyStore
{
    public sealed class TokenlessKeyStoreException : Exception
    {
        public TokenlessKeyStoreException(string message) : base(message)
        {
        }
    }
}
