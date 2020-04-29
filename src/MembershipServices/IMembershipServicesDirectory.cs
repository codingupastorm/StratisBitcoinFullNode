using NBitcoin;
using Org.BouncyCastle.X509;

namespace MembershipServices
{
    public interface IMembershipServicesDirectory
    {
        void Initialize();

        X509Certificate GetCertificateForAddress(uint160 address);

        X509Certificate GetCertificateForTransactionSigningPubKeyHash(byte[] transactionSigningPubKeyHash);

        bool IsCertificateRevoked(string thumbprint);

        /// <summary>
        /// Tries to determine if a certificate is revoked by checking the transaction signing key of the node that signed the certificate.
        /// </summary>
        /// <param name="base64PubKeyHash">This is usually the node's transaction signing key in base64 form.</param>
        /// <returns><c>True</c> if the status is not revoked, otherwise false.</returns>
        bool IsCertificateRevokedByTransactionSigningKeyHash(byte[] pubKeyHash);

        /// <summary>
        /// Determines whether a certificate has been revoked by checking the sender (node)'s address.
        /// </summary>
        /// <param name="address">The address of the node.</param>
        /// <returns><c>true</c> if the given certificate has been revoked.</returns>
        bool IsCertificateRevokedByAddress(uint160 address);

        /// <summary>
        /// A helper method to place a certificate into the local MSD.
        /// </summary>
        /// <param name="memberCertificate">The certificate associated with the member.</param>
        /// <param name="memberType">Determines which subfolder the certificate will be placed in, according to the permission level.</param>
        /// <returns>Whether or not the member was successfully added.</returns>
        bool AddLocalMember(X509Certificate memberCertificate, MemberType memberType);

        /// <summary>
        /// A helper method to remove a certificate from the local MSD.
        /// </summary>
        /// <param name="memberCertificate">The certificate associated with the member.</param>
        /// <param name="memberType">Determines which subfolder the certificate will be removed from, according to the permission level.</param>
        /// <returns>Whether or not the member was successfully removed.</returns>
        bool RemoveLocalMember(X509Certificate memberCertificate, MemberType memberType);

        bool AddChannelMember(X509Certificate memberCertificate, string channelId, MemberType memberType);

        bool RemoveChannelMember(X509Certificate memberCertificate, string channelId, MemberType memberType);

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        bool IsSignedByAuthorityCertificate(X509Certificate certificateToValidate, X509Certificate authorityCertificate);
    }
}
