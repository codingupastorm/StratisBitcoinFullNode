using System;
using System.Security.Cryptography.X509Certificates;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificateChecker
    {
        bool CheckSenderCertificateHasPermission(uint160 address);
    }
    public class CertificateChecker : ICertificateChecker
    {
        private readonly ICertificateCache certificateCache;

        public CertificateChecker(ICertificateCache certificateCache)
        {
            this.certificateCache = certificateCache;
        }

        public bool CheckSenderCertificateHasPermission(uint160 address)
        {
            // Firstly we need to get the certificate.
            X509Certificate2 certificate = this.certificateCache.GetCertificate(address);

            if (certificate == null)
                certificate = GetCertificateFromCA(address);

            // Now we have the certificate, validate it has the required permissions.
            if (certificate == null)
                return false;


            throw new NotImplementedException();
        }

        private X509Certificate2 GetCertificateFromCA(uint160 address)
        {
            throw new NotImplementedException();
        }
    }
}
