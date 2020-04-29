using CertificateAuthority;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificatePermissionsChecker
    {
        /// <summary>
        /// Determines whether or not the node's own certificate contains a given permission.
        /// </summary>
        /// <para>
        /// The CA will not be contacted if the certificate is not present.
        /// </para>
        /// <param name="oId">The permission we're checking for.</param>
        /// <returns>Whether or not is has the required permission.</returns>
        bool CheckOwnCertificatePermission(string oId);

        /// <summary>
        /// Determines whether a given sender has the permission required to send transactions on the network by
        /// checking their certificate. If the certificate isn't known and stored locally, it will be retrieved from the CA.
        /// </summary>
        /// <param name="address">The sender that is trying to send a transaction.</param>
        /// <param name="permission">The permission we're checking for.</param>
        /// <returns>Whether or not they have the required permissions to send a transaction.</returns>
        bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission);
    }

    public sealed class CertificatePermissionsChecker : ICertificatePermissionsChecker
    {
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly ICertificatesManager certificatesManager;
        private readonly ILogger logger;

        public CertificatePermissionsChecker(
            IMembershipServicesDirectory membershipServices,
            ICertificatesManager certificatesManager,
            ILoggerFactory loggerFactory)
        {
            this.membershipServices = membershipServices;
            this.certificatesManager = certificatesManager;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <inheritdoc />
        public bool CheckOwnCertificatePermission(string oId)
        {
            // We don't have our own certificate so return false as the required permission cannot be determined.
            if (this.certificatesManager.ClientCertificate == null)
                return false;

            byte[] permissionBytes = MembershipServicesDirectory.ExtractCertificateExtension(this.certificatesManager.ClientCertificate, oId);
            return permissionBytes != null;
        }

        /// <inheritdoc />
        public bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission)
        {
            X509Certificate certificate = this.GetCertificate(address);
            return ValidateCertificateHasPermission(certificate, permission);
        }

        private X509Certificate GetCertificate(uint160 address)
        {
            // The certificate might be our own. If so, just return that one, no need to get from the cache or query CA.
            if (this.certificatesManager?.ClientCertificate != null)
            {
                // TODO: This value could be cached, or retrieved from the wallet?
                byte[] myCertTransactionSigningHash = MembershipServicesDirectory.ExtractCertificateExtension(this.certificatesManager.ClientCertificate, CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid);
                var myCertAddress = new uint160(myCertTransactionSigningHash);

                if (myCertAddress == address)
                {
                    return this.certificatesManager.ClientCertificate;
                }
            }

            return this.membershipServices.GetCertificateForAddress(address);
        }

        public static bool ValidateCertificateHasPermission(X509Certificate certificate, TransactionSendingPermission permission)
        {
            if (certificate == null)
                return false;

            string oidToCheckFor = permission.GetPermissionOid();

            byte[] result = MembershipServicesDirectory.ExtractCertificateExtension(certificate, oidToCheckFor);
            return result != null && result[0] == 1;
        }
    }
}
