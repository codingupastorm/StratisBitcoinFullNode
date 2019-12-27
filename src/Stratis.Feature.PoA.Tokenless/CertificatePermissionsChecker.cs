using System;
using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificatePermissionsChecker
    {
        bool CheckSenderCertificateHasPermission(uint160 address);
    }
    public class CertificatePermissionsChecker : ICertificatePermissionsChecker
    {
        public const string SendPermission = "SendPermission";

        private readonly ICertificateCache certificateCache;

        public CertificatePermissionsChecker(ICertificateCache certificateCache)
        {
            this.certificateCache = certificateCache;
        }

        public bool CheckSenderCertificateHasPermission(uint160 address)
        {
            X509Certificate2 certificate = this.GetCertificate(address); 
            return ValidateCertificateHasPermission(certificate);
        }

        private X509Certificate2 GetCertificate(uint160 address)
        {
            X509Certificate2 certificate = this.certificateCache.GetCertificate(address);

            if (certificate == null)
                certificate = GetCertificateFromCA(address);

            return certificate;
        }

        private X509Certificate2 GetCertificateFromCA(uint160 address)
        {
            throw new NotImplementedException("Functionality to query CA isn't ready yet.");
        }

        public static bool ValidateCertificateHasPermission(X509Certificate2 certificate)
        {
            // TODO: Can easily be extended to check for different permission types.

            if (certificate == null)
                return false;

            byte[] result = CertificatesManager.ExtractCertificateExtension(certificate, SendPermission);
            return result.Length != 0;
        }
    }
}
