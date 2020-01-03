using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificatePermissionsChecker
    {
        /// <summary>
        /// Determines whether a given sender has the permission required to send transactions on the network by
        /// checking their certificate. If the certificate isn't known and stored locally, it will be retrieved from the CA.
        /// </summary>
        /// <param name="address">The sender that is trying to send a transaction.</param>
        /// <param name="permission">The permission we're checking for.</param>
        /// <returns>Whether or not they have the required permissions to send a transaction.</returns>
        bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission);
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

        /// <inheritdoc />
        public bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission)
        {
            X509Certificate2 certificate = this.GetCertificate(address); 
            return ValidateCertificateHasPermission(certificate, permission);
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

        public static bool ValidateCertificateHasPermission(X509Certificate2 certificate, TransactionSendingPermission permission)
        {
            if (certificate == null)
                return false;

            string oidToCheckFor = permission.GetPermissionOid();

            byte[] result = CertificatesManager.ExtractCertificateExtension(certificate, oidToCheckFor);
            return result != null && result[0] == 1;
        }
    }
}
