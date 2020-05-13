using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Stratis.Core.Configuration;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = System.Security.Cryptography.X509Certificates.X509Extension;

namespace MembershipServices
{
    public enum MemberType
    {
        NetworkPeer,
        Admin,
        IntermediateCA,
        RootCA,
        Self
    }

    /// <summary>
    /// The role for a member is encoded within its certificate, as the first of the organisational unit (OU) values in the subject distinguished name.
    /// It is valid for there to be multiple OU= values in the subject.
    /// The HL config allows flexibility in naming for the OU, i.e. mapping an arbitrary string to represent the 'client' role.
    /// This has currently not been implemented as it is unnecessary. It appears to mainly be for facilitating enforcing different CA certificates signing for different roles.
    /// </summary>
    /// <remarks>This is currently more of a placeholder, as we are not using these roles to define any permissions yet (instead, we are still using the permissions encoded into the certificates themselves).</remarks>
    public enum RoleType
    {
        Client,
        Peer,
        Admin,
        //Orderer - Currently there is no real need for the orderer role as our consensus happens via a different mechanism
    }

    public class MembershipServicesDirectory : IMembershipServicesDirectory
    {
        /// <inheritdoc/>
        public X509Certificate AuthorityCertificate { get; private set; }

        /// <inheritdoc/>
        public X509Certificate ClientCertificate { get; private set; }

        /// <inheritdoc/>
        public AsymmetricKeyParameter ClientCertificatePrivateKey { get; private set; }

        public CertificateAuthorityInterface CertificateAuthorityInterface { get; }

        private readonly NodeSettings nodeSettings;

        private readonly ILogger logger;

        private readonly Stratis.Core.Configuration.TextFileConfiguration configuration;

        private readonly LocalMembershipServicesConfiguration localMembershipServices;

        // A mapping of channel identifiers to their corresponding membership services configuration.
        // As channels do not really exist yet, the identifier format is yet to be defined.
        private readonly Dictionary<string, ChannelMembershipServicesConfiguration> channelMembershipServices;

        public MembershipServicesDirectory(NodeSettings nodeSettings, ILoggerFactory loggerFactory)
        {
            this.nodeSettings = nodeSettings;
            this.configuration = nodeSettings.ConfigReader;

            this.logger = loggerFactory.CreateLogger(GetType().FullName);

            // Two types of providers needed:
            // 1. Local
            // 2. Channel

            // Local - every node needs one defined. Stored on the local filesystem as a pre-defined folder structure.
            // There is only one local provider on any given node.
            // Need all certificates to be signed by the CA so that each node can independently verify the veracity of the certificates it has been provided with.

            // https://github.com/hyperledger/fabric/blob/master/docs/source/msp.rst
            // https://github.com/hyperledger/fabric-sdk-go/blob/master/internal/github.com/hyperledger/fabric/msp/msp.go

            this.localMembershipServices = new LocalMembershipServicesConfiguration(Path.Combine(this.nodeSettings.DataDir, this.nodeSettings.Network.RootFolderName, this.nodeSettings.Network.Name), this.nodeSettings.Network);

            // Channel - defines administrative and participatory rights at the channel level. Defined in a channel configuration JSON (in the HL design).
            // Instantiated on the file system of every node in the channel (similar to local version, but there can be multiple providers for a channel) and kept synchronized via consensus.

            this.channelMembershipServices = new Dictionary<string, ChannelMembershipServicesConfiguration>();

            this.CertificateAuthorityInterface = new CertificateAuthorityInterface(this.nodeSettings, loggerFactory);

            this.AuthorityCertificate = this.CertificateAuthorityInterface.LoadAuthorityCertificate();
        }

        public void Initialize()
        {
            (this.ClientCertificate, this.ClientCertificatePrivateKey) = this.CertificateAuthorityInterface.LoadClientCertificate(this.AuthorityCertificate);

            if (this.ClientCertificate == null)
            {
                this.logger.LogError($"Please generate the node's certificate with the MembershipServices.Cli utility.");

                throw new CertificateConfigurationException($"Please generate the node's certificate with the MembershipServices.Cli utility.");
            }

            // We attempt to set up the folder structure regardless of whether it has been done already.
            LocalMembershipServicesConfiguration.InitializeFolderStructure(Path.Combine(this.nodeSettings.DataDir, this.nodeSettings.Network.RootFolderName, this.nodeSettings.Network.Name));
            this.localMembershipServices.InitializeExistingCertificates();

            bool revoked = this.IsCertificateRevoked(CaCertificatesManager.GetThumbprint(this.ClientCertificate));

            if (revoked)
                throw new CertificateConfigurationException("Provided client certificate was revoked!");
        }

        /// <inheritdoc />
        public bool AddLocalMember(X509Certificate memberCertificate, MemberType memberType)
        {
            return this.localMembershipServices.AddCertificate(memberCertificate, memberType);
        }

        public bool RemoveLocalMember(X509Certificate memberCertificate, MemberType memberType)
        {
            return this.localMembershipServices.RemoveCertificate(memberCertificate, memberType);
        }

        public bool AddChannelMember(X509Certificate memberCertificate, string channelId, MemberType memberType)
        {
            throw new NotImplementedException();
        }

        public bool RemoveChannelMember(X509Certificate memberCertificate, string channelId, MemberType memberType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks if given certificate is signed by the authority certificate.
        /// </summary>
        /// <exception cref="Exception">Thrown in case authority chain build failed.</exception>
        public static bool IsSignedByAuthorityCertificate(X509Certificate certificateToValidate, X509Certificate authorityCertificate)
        {
            return CaCertificatesManager.ValidateCertificateChain(authorityCertificate, certificateToValidate);
        }

        public X509Certificate GetCertificateForThumbprint(string thumbprint)
        {
            return this.localMembershipServices.GetCertificateByThumbprint(thumbprint);
        }

        public X509Certificate GetCertificateForAddress(uint160 address)
        {
            var p2pkh = new BitcoinPubKeyAddress(new KeyId(address), this.nodeSettings.Network);

            return this.localMembershipServices.GetCertificateByAddress(p2pkh.ToString());
        }

        public X509Certificate GetCertificateForTransactionSigningPubKeyHash(byte[] transactionSigningPubKeyHash)
        {
            return this.localMembershipServices.GetCertificateByTransactionSigningPubKeyHash(transactionSigningPubKeyHash);
        }

        public void RevokeCertificate(string thumbprint)
        {
            this.localMembershipServices.RevokeCertificate(thumbprint);
        }

        // TODO: Perhaps move revocation checking into a sub-component of the MSD to keep the top level cleaner.
        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.localMembershipServices.IsCertificateRevoked(thumbprint);
        }

        public bool IsCertificateRevokedByTransactionSigningKeyHash(byte[] pubKeyHash)
        {
            X509Certificate certificate = this.GetCertificateForTransactionSigningPubKeyHash(pubKeyHash);

            // If the certificate is unknown to us, assume revocation.
            if (certificate == null)
                return true;

            string thumbprint = GetCertificateThumbprint(certificate);

            return this.IsCertificateRevoked(thumbprint);
        }

        public bool IsCertificateRevokedByAddress(uint160 address)
        {
            return this.IsCertificateRevokedByTransactionSigningKeyHash(address.ToBytes());
        }

        public List<PubKey> GetCertificatePublicKeys()
        {
            // TODO: Have the option to return this from the local MSD instead so that a CA connection isn't necessary for federation membership synchronization
            return this.CertificateAuthorityInterface.GetCertificatePublicKeys();
        }

        public static string GetCertificateThumbprint(X509Certificate certificate)
        {
            byte[] certificateBytes = certificate.GetEncoded();
            var hash = new Sha1Digest();
            hash.BlockUpdate(certificateBytes, 0, certificateBytes.Length);
            byte[] result = new byte[hash.GetDigestSize()];
            hash.DoFinal(result, 0);

            return BitConverter.ToString(result).Replace("-", string.Empty);
        }

        public static string GetCertificateCommonName(X509Certificate certificate)
        {
            X509Name subject = PrincipalUtilities.GetSubjectX509Principal(certificate);
            IList commonNames = subject.GetValueList(X509Name.CN);

            return commonNames.Count > 0 ? commonNames[0].ToString() : null;
        }

        /// <summary>
        /// This is NOT the address of the P2P private key, stored in the P2PKH extension.
        /// It is instead the address corresponding to the transaction signing pubkey hash.
        /// </summary>
        public static string GetCertificateTransactionSigningAddress(X509Certificate certificate, Network network)
        {
            // TODO: This implementation is directly from CertificatesManager. Need to avoid the duplication.

            // TODO: Find a way of extracting this extension cleanly without a trip through X509Certificate2.
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid)
                {
                    var temp = extension.RawData.Skip(2).ToArray();

                    var address = new BitcoinPubKeyAddress(new KeyId(temp), network);

                    return address.ToString();
                }
            }

            return null;
        }

        public static byte[] ExtractCertificateExtension(X509Certificate certificate, string oid)
        {
            // TODO: Find a way of extracting this extension cleanly without a trip through X509Certificate2.
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                    // This is truly horrible, but it isn't clear how we can correctly go from the DER bytes in the extension, to a relevant BC class, to a string.
                    // Perhaps we are meant to recursively evaluate the extension data as ASN.1 until we land up with raw data that can't be decoded further?
                    // IMPORTANT: The two prefix bytes being removed consist of a type tag (e.g. `0x04` = octet string)
                    // and a length byte. For lengths > 127 more than one byte is needed, which would break this code.
                    return extension.RawData.Skip(2).ToArray();
            }

            return null;
        }

        public byte[] ExtractCertificateExtensionFromOid(X509Certificate certificate, string oid)
        {
            // TODO: This is an unfortunate hack needed to simplify testing.
            return ExtractCertificateExtension(certificate, oid);
        }

        public static string ExtractCertificateExtensionString(X509Certificate certificate, string oid)
        {
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == oid)
                {
                    // This is truly horrible, but it isn't clear how we can correctly go from the DER bytes in the extension, to a relevant BC class, to a string.
                    // Perhaps we are meant to recursively evaluate the extension data as ASN.1 until we land up with raw data that can't be decoded further?
                    var temp = extension.RawData.Skip(2).ToArray();

                    return Encoding.UTF8.GetString(temp);
                }
            }

            return null;
        }
    }
}
