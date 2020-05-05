using System.Collections.Generic;
using System.Linq;
using CertificateAuthority;
using CertificateAuthority.Models;
using MembershipServices;
using NBitcoin;
using NBitcoin.Crypto;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;

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
        bool CheckSenderCertificateHasPermission(uint160 address, string permissionOid);

        /// <summary>
        /// Determines whether a given sender has the permission required to send transactions on the network by
        /// checking their certificate. If the certificate isn't known and stored locally, it will be retrieved from the CA.
        /// </summary>
        /// <param name="address">The sender that is trying to send a transaction.</param>
        /// <param name="permission">The permission we're checking for.</param>
        /// <returns>Whether or not they have the required permissions to send a transaction.</returns>
        bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission);

        /// <summary>
        /// Determines whether a given sender has the right to be on a given channel.
        /// </summary>
        /// <param name="address">The sender that is trying to send a transaction.</param>
        /// <param name="network">The channel we're checking on.</param>
        /// <returns>Whether or not they are allowed to be on this channel.</returns>
        bool CheckSenderCertificateIsPermittedOnChannel(uint160 address, ChannelNetwork network);

        /// <summary>
        /// Used to validate that the signature has been signed by the transaction signing pubkey corresponding to the given certificate.
        /// </summary>
        /// <param name="certificate"></param>
        /// <param name="signature"></param>
        /// <param name="hash"></param>
        /// <returns></returns>
        bool CheckSignature(string certificateThumbprint, ECDSASignature signature, PubKey pubKey, uint256 hash);
    }

    public sealed class CertificatePermissionsChecker : ICertificatePermissionsChecker
    {
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly ICertificatesManager certificatesManager;

        public CertificatePermissionsChecker(
            IMembershipServicesDirectory membershipServices,
            ICertificatesManager certificatesManager)
        {
            this.membershipServices = membershipServices;
            this.certificatesManager = certificatesManager;
        }

        /// <inheritdoc />
        public bool CheckOwnCertificatePermission(string oId)
        {
            // We don't have our own certificate so return false as the required permission cannot be determined.
            if (this.certificatesManager.ClientCertificate == null)
                return false;

            byte[] permissionBytes = CertificatesManager.ExtractCertificateExtension(this.certificatesManager.ClientCertificate, oId);
            return permissionBytes != null;
        }

        /// <inheritdoc />
        public bool CheckSenderCertificateHasPermission(uint160 address, string permissionOid)
        {
            X509Certificate certificate = this.GetCertificate(address);
            return ValidateCertificateHasPermission(certificate, permissionOid);
        }

        /// <inheritdoc />
        public bool CheckSenderCertificateHasPermission(uint160 address, TransactionSendingPermission permission)
        {
            X509Certificate certificate = this.GetCertificate(address);
            return ValidateCertificateHasPermission(certificate, permission);
        }

        public bool CheckSenderCertificateIsPermittedOnChannel(uint160 address, ChannelNetwork network)
        {
            X509Certificate certificate = this.GetCertificate(address);
            return ValidateCertificatePermittedOnChannel(certificate, network);
        }

        /// <inheritdoc />
        public bool CheckSignature(string certificateThumbprint, ECDSASignature signature, PubKey pubKey, uint256 hash)
        {
            List<CertificateInfoModel> certificates = this.certificatesManager.GetAllCertificates();

            CertificateInfoModel match = certificates.FirstOrDefault(c => c.Thumbprint == certificateThumbprint);

            if (match == null) return false;

            // Verify the pubkey matches the hash.
            if (new KeyId(match.TransactionSigningPubKeyHash) != pubKey.Hash)
                return false;

            return pubKey.Verify(hash, signature);
        }

        private X509Certificate GetCertificate(uint160 address)
        {
            // The certificate might be our own. If so, just return that one, no need to get from the cache or query CA.
            if (this.certificatesManager?.ClientCertificate != null)
            {
                // TODO: This value could be cached, or retrieved from the wallet?
                byte[] myCertTransactionSigningHash = CertificatesManager.ExtractCertificateExtension(this.certificatesManager.ClientCertificate, CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid);
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
            return ValidateCertificateHasPermission(certificate, permission.GetPermissionOid());
        }

        private static bool ValidateCertificatePermittedOnChannel(X509Certificate certificate, ChannelNetwork network)
        {
            // In future iterations we will add complexity around who is allowed on a channel here.

            return certificate.GetOrganisation() == network.Organisation;
        }

        private static bool ValidateCertificateHasPermission(X509Certificate certificate, string permissionOid)
        {
            if (certificate == null)
                return false;

            byte[] result = CertificatesManager.ExtractCertificateExtension(certificate, permissionOid);
            return result != null && result[0] == 1;
        }
    }
}
