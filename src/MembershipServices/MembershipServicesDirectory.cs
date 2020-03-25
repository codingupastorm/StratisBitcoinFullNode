using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CertificateAuthority;
using NBitcoin;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
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
        private readonly NodeSettings nodeSettings;

        private readonly LocalMembershipServicesConfiguration localMembershipServices;
        
        // A mapping of channel identifiers to their corresponding membership services configuration.
        // As channels do not really exist yet, the identifier format is yet to be defined.
        private readonly Dictionary<string, ChannelMembershipServicesConfiguration> channelMembershipServices;

        public MembershipServicesDirectory(NodeSettings nodeSettings)
        {
            this.nodeSettings = nodeSettings;

            // Two types of providers needed:
            // 1. Local
            // 2. Channel

            // Local - every node needs one defined. Stored on the local filesystem as a pre-defined folder structure.
            // There is only one local provider on any given node.
            // Need all certificates to be signed by the CA so that each node can independently verify the veracity of the certificates it has been provided with.

            // https://github.com/hyperledger/fabric/blob/master/docs/source/msp.rst
            // https://github.com/hyperledger/fabric-sdk-go/blob/master/internal/github.com/hyperledger/fabric/msp/msp.go

            this.localMembershipServices = new LocalMembershipServicesConfiguration(this.nodeSettings.DataDir, this.nodeSettings.Network);

            // Channel - defines administrative and participatory rights at the channel level. Defined in a channel configuration JSON (in the HL design).
            // Instantiated on the file system of every node in the channel (similar to local version, but there can be multiple providers for a channel) and kept synchronized via consensus.

            this.channelMembershipServices = new Dictionary<string, ChannelMembershipServicesConfiguration>();
        }
        
        public void Initialize()
        {
            // We attempt to set up the folder structure regardless of whether it has been done already.
            this.localMembershipServices.InitializeFolderStructure();
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

        public X509Certificate GetCertificateForAddress(uint160 address)
        {
            var p2pkh = new BitcoinPubKeyAddress(new KeyId(address), this.nodeSettings.Network);

            return this.localMembershipServices.GetCertificateByAddress(p2pkh.ToString());
        }

        public X509Certificate GetCertificateForTransactionSigningPubKeyHash(byte[] transactionSigningPubKeyHash)
        {
            return this.localMembershipServices.GetCertificateByTransactionSigningPubKeyHash(transactionSigningPubKeyHash);
        }

        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.localMembershipServices.IsCertificateRevoked(thumbprint);
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

        public static byte[] GetTransactionSigningPubKeyHash(X509Certificate certificate)
        {
            // TODO: This implementation is directly from CertificatesManager. Need to avoid the duplication.

            // TODO: Find a way of extracting this extension cleanly without a trip through X509Certificate2.
            X509Certificate2 cert = CaCertificatesManager.ConvertCertificate(certificate, new SecureRandom());

            foreach (X509Extension extension in cert.Extensions)
            {
                if (extension.Oid.Value == CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid)
                    return extension.RawData.Skip(2).ToArray();
            }

            return null;
        }
    }
}
