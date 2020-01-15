using System;

namespace CertificateAuthority
{
    public sealed class CertificateAuthorityAccountException : Exception
    {
        public CertificateAuthorityAccountException(string message) : base(message)
        {
        }
    }
}
