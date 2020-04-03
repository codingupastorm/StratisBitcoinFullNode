using System;

namespace Stratis.Features.Wallet
{
    public class CannotAddAccountToXpubKeyWalletException : Exception
    {
        public CannotAddAccountToXpubKeyWalletException(string message) : base(message)
        {
        }
    }
}