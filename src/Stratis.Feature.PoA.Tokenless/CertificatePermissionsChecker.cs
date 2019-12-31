using System.Security.Cryptography.X509Certificates;
using CertificateAuthority;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificatePermissionsChecker
    {
        bool CheckSenderCertificateHasPermission(uint160 address);
    }
    public class CertificatePermissionsChecker : ICertificatePermissionsChecker
    {
        private readonly ICertificateCache certificateCache;
        private readonly CertificatesManager certificatesManager;
        private readonly Network network;

        public CertificatePermissionsChecker(ICertificateCache certificateCache,
            CertificatesManager certificatesManager,
            Network network)
        {
            this.certificateCache = certificateCache;
            this.certificatesManager = certificatesManager;
            this.network = network;
        }

        public bool CheckSenderCertificateHasPermission(uint160 address)
        {
            X509Certificate2 certificate = this.GetCertificate(address); 
            return ValidateCertificateHasPermission(certificate);
        }

        private X509Certificate2 GetCertificate(uint160 address)
        {
            X509Certificate2 certificate = this.certificateCache.GetCertificate(address) 
                                           ?? GetCertificateFromCA(address);

            return certificate;
        }

        private X509Certificate2 GetCertificateFromCA(uint160 address)
        {
            return this.certificatesManager.GetCertificateForAddress(address.ToBase58Address(this.network));
        }

        public static bool ValidateCertificateHasPermission(X509Certificate2 certificate)
        {
            // TODO: Can easily be extended to check for different permission types.

            if (certificate == null)
                return false;

            byte[] result = CertificatesManager.ExtractCertificateExtension(certificate, CaCertificatesManager.SendPermission);
            return result != null;
        }
    }
}
