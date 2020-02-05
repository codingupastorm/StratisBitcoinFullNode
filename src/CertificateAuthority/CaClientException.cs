using System;

namespace CertificateAuthority
{
    public class CaClientException : Exception
    {
        public CaClientException(string message) : base(message) { }
    }
}
