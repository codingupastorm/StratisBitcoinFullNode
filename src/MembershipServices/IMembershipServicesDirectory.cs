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
    }
}
