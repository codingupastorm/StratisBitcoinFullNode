using System;

namespace CertificateAuthority
{
    public class CaClientException : Exception
    {
        public CaClientException(string message) : base(message) { }

        public CaClientException(string message, Exception exception) : base(message, exception) { }
    }
}
