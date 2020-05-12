using System;
using System.Collections.Generic;
using NBitcoin;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace MembershipServices
{
    public interface IMembershipServicesDirectory
    {
        /// <summary>Root certificate of the certificate authority for the current network.</summary>
        X509Certificate AuthorityCertificate { get; }

        /// <summary>Client certificate that is used to establish connections with other peers.</summary>
        X509Certificate ClientCertificate { get; }

        /// <summary>The private key associated with the loaded client certificate. Intended to be used for TLS communication only.</summary>
        AsymmetricKeyParameter ClientCertificatePrivateKey { get; }

        CertificateAuthorityInterface CertificateAuthorityInterface { get; }

        void Initialize();

        X509Certificate GetCertificateForThumbprint(string thumbprint);

        X509Certificate GetCertificateForAddress(uint160 address);

        X509Certificate GetCertificateForTransactionSigningPubKeyHash(byte[] transactionSigningPubKeyHash);

        List<PubKey> GetCertificatePublicKeys();

        void RevokeCertificate(string thumbprint);

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

        byte[] ExtractCertificateExtensionFromOid(X509Certificate certificate, string oid);
    }
}
