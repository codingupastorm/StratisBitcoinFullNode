using System;
using MembershipServices;
using Org.BouncyCastle.X509;

namespace Stratis.Feature.PoA.Tokenless.ProtocolEncryption
{
    public interface IRevocationChecker : IDisposable
    {
        /// <summary>
        /// As there can be multiple certificates in existence for a given P2PKH address (i.e. e.g. with different expiry dates), we determine revocation via thumbprint.
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to check the revocation status of.</param>
        /// <returns><c>true</c> if the given certificate has been revoked.</returns>
        bool IsCertificateRevoked(string thumbprint);

        /// <summary>
        /// Tries to determine if a certificate is revoked by checking the transaction signing key of the node that signed the certificate.
        /// </summary>
        /// <param name="base64PubKeyHash">This is usually the node's transaction signing key in base64 form.</param>
        /// <returns><c>True</c> if the status is not revoked, otherwise false.</returns>
        bool IsCertificateRevokedByTransactionSigningKeyHash(byte[] pubKeyHash);

        void Initialize();
    }

    public sealed class RevocationChecker : IRevocationChecker
    {
        private readonly IMembershipServicesDirectory membershipServices;

        public RevocationChecker(
            IMembershipServicesDirectory membershipServices)
        {
            this.membershipServices = membershipServices;
        }

        public void Initialize()
        {
            // TODO: We probably don't need this class at all going forwards, and can just interrogate the MSD directly instead. It has been left in for now to reduce the quantity of initial edits required.
        }

        /// <inheritdoc/>
        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.membershipServices.IsCertificateRevoked(thumbprint);
        }

        /// <inheritdoc/>
        public bool IsCertificateRevokedByTransactionSigningKeyHash(byte[] pubKeyHash)
        {
            X509Certificate certificate = this.membershipServices.GetCertificateForTransactionSigningPubKeyHash(pubKeyHash);

            // If the certificate is unknown to us, assume revocation.
            if (certificate == null)
                return true;

            string thumbprint = MembershipServicesDirectory.GetCertificateThumbprint(certificate);

            return this.membershipServices.IsCertificateRevoked(thumbprint);
        }

        public void Dispose()
        {
        }
    }
}
