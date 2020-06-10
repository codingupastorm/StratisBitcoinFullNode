using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using CertificateAuthority;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Core.Utilities;

namespace MembershipServices
{
    public class LocalMembershipServicesConfiguration
    {
        private readonly Network network;
        private readonly string rootFolder;

        /// <summary>
        /// Subfolder holding certificate files each corresponding to an administrator certificate.
        /// </summary>
        private readonly string AdminCerts = @"admincerts";

        /// <summary>
        /// Subfolder holding certificate files each corresponding to an intermediate CA's certificate.
        /// </summary>
        /// <remarks>Optional.</remarks>
        private readonly string IntermediateCerts = @"intermediatecerts";

        /// <summary>
        /// Subfolder holding the considered CRLs (certificate revocation lists).
        /// </summary>
        /// <remarks>Optional.</remarks>
        private readonly string Crls = @"crls";

        /// <summary>
        /// Subfolder holding certificate files each corresponding to a root CA's certificate.
        /// </summary>
        private readonly string CaCerts = @"cacerts";

        /// <summary>
        /// Subfolder holding certificate files for the peers the node is aware of. This is primarily for validating transaction signing, as P2P certificates do not yet require a central registry.
        /// </summary>
        /// <remarks>This is somewhat a stopgap solution until channels are properly implemented, as channels could be established between peers of (potentially) different organisations.
        /// It is also so that transaction validation/endorsement can be correctly performed, as a transaction signature does not contain any certificate information. So the MSD
        /// will have to be responsible for mapping the sender address in a transaction to a certificate stored in this folder. Further research about exactly how HL does this is required.</remarks>
        private readonly string PeerCerts = @"peercerts";

        /// <summary>
        /// Subfolder holding a file with the node's X.509 certificate (public key).
        /// </summary>
        private readonly string SignCerts = @"signcerts";

        /// <summary>
        /// An identifier for this local MSD.
        /// </summary>
        public string Identifier { get; set; }

        // https://hyperledger-fabric.readthedocs.io/en/release-2.0/membership/membership.html#msp-structure

        /*
        (optional) a folder tlscacerts to include certificate files each corresponding to a TLS root CA's certificate
        (optional) a folder tlsintermediatecerts to include certificate files each corresponding to an intermediate TLS CA's certificate
        */

        private readonly ConcurrentDictionary<string, X509Certificate> mapThumbprints;
        private readonly ConcurrentDictionary<string, X509Certificate> mapCommonNames;
        private readonly ConcurrentDictionary<string, X509Certificate> mapAddresses;
        private readonly ConcurrentDictionary<byte[], X509Certificate> mapTransactionSigningPubKeyHash;
        private readonly HashSet<string> revokedCertificateThumbprints;

        public LocalMembershipServicesConfiguration(string folder, Network network)
        {
            // TODO: Use the identifier in the base path. Specify the local MSD ID on first startup?
            this.rootFolder = Path.Combine(folder, "msd");

            this.network = network;

            this.mapThumbprints = new ConcurrentDictionary<string, X509Certificate>();
            this.mapCommonNames = new ConcurrentDictionary<string, X509Certificate>();
            this.mapAddresses = new ConcurrentDictionary<string, X509Certificate>();
            this.mapTransactionSigningPubKeyHash = new ConcurrentDictionary<byte[], X509Certificate>(new ByteArrayComparer());
            this.revokedCertificateThumbprints = new HashSet<string>();
        }

        public bool AddCertificate(X509Certificate certificate, MemberType memberType)
        {
            string pathToCert = GetCertificatePath(memberType, certificate);

            try
            {
                File.WriteAllBytes(pathToCert, certificate.GetEncoded());

                this.PutCertificateIntoMappings(certificate);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool RemoveCertificate(X509Certificate certificate, MemberType memberType)
        {
            string pathToCert = GetCertificatePath(memberType, certificate);

            // We are using the return result to indicate if the certificate was deleted. So if it never existed in the first place, it wasn't deleted.
            if (!File.Exists(pathToCert))
                return false;

            try
            {
                File.Delete(pathToCert);

                this.mapThumbprints.TryRemove(MembershipServicesDirectory.GetCertificateThumbprint(certificate), out _);
                this.mapCommonNames.TryRemove(MembershipServicesDirectory.GetCertificateCommonName(certificate), out _);
                this.mapAddresses.TryRemove(MembershipServicesDirectory.GetCertificateTransactionSigningAddress(certificate, this.network), out _);
                this.mapTransactionSigningPubKeyHash.TryRemove(MembershipServicesDirectory.ExtractCertificateExtension(certificate, CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid), out _);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public X509Certificate GetCertificateByThumbprint(string thumbprint)
        {
            this.mapThumbprints.TryGetValue(thumbprint, out X509Certificate certificate);

            return certificate;
        }

        public X509Certificate GetCertificateByCommonName(string commonName)
        {
            this.mapCommonNames.TryGetValue(commonName, out X509Certificate certificate);

            return certificate;
        }

        public X509Certificate GetCertificateByAddress(string address)
        {
            this.mapAddresses.TryGetValue(address, out X509Certificate certificate);

            return certificate;
        }

        public X509Certificate GetCertificateByTransactionSigningPubKeyHash(byte[] transactionSigningPubKeyHash)
        {
            this.mapTransactionSigningPubKeyHash.TryGetValue(transactionSigningPubKeyHash, out X509Certificate certificate);

            return certificate;
        }

        public void RevokeCertificate(string thumbprint)
        {
            this.revokedCertificateThumbprints.Add(thumbprint);

            // We don't actually need to store the certificate, only a record of its thumbprint.
            FileStream file = File.Create(Path.Combine(GetCertificatePath(MemberType.Revocation), thumbprint));
            file.Flush();
            file.Dispose();

            X509Certificate certificate = this.GetCertificateByThumbprint(thumbprint);

            this.RemoveCertificate(certificate, MemberType.NetworkPeer);
        }

        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.revokedCertificateThumbprints.Contains(thumbprint);
        }

        public void InitializeFolderStructure()
        {
            Directory.CreateDirectory(this.rootFolder);

            Directory.CreateDirectory(GetCertificatePath(MemberType.Admin));
            Directory.CreateDirectory(GetCertificatePath(MemberType.RootCA));
            Directory.CreateDirectory(GetCertificatePath(MemberType.IntermediateCA));
            Directory.CreateDirectory(GetCertificatePath(MemberType.Revocation));
            Directory.CreateDirectory(GetCertificatePath(MemberType.SelfSign));
            Directory.CreateDirectory(GetCertificatePath(MemberType.NetworkPeer));
        }

        public void InitializeExistingCertificates()
        {
            foreach (string fileName in Directory.GetFiles(GetCertificatePath(MemberType.Revocation)))
            {
                this.revokedCertificateThumbprints.Add(Path.GetFileName(fileName));
            }

            // There will probably only be one certificate in this folder, but nevertheless, load it into the lookups.
            foreach (string fileName in Directory.GetFiles(GetCertificatePath(MemberType.SelfSign)))
            {
                try
                {
                    var parser = new X509CertificateParser();

                    X509Certificate certificate = parser.ReadCertificate(File.ReadAllBytes(fileName));

                    // TODO: Maybe the certificate should actually be deleted from disk here if it is known to be revoked
                    if (this.revokedCertificateThumbprints.Contains(MembershipServicesDirectory.GetCertificateThumbprint(certificate)))
                        continue;

                    this.PutCertificateIntoMappings(certificate);
                }
                catch (Exception)
                {
                    // TODO: Log potentially corrupted certificate files in the signcerts folder
                }
            }

            foreach (string fileName in Directory.GetFiles(GetCertificatePath(MemberType.NetworkPeer)))
            {
                try
                {
                    var parser = new X509CertificateParser();

                    X509Certificate certificate = parser.ReadCertificate(File.ReadAllBytes(fileName));

                    // TODO: Maybe the certificate should actually be deleted from disk here if it is known to be revoked
                    if (this.revokedCertificateThumbprints.Contains(MembershipServicesDirectory.GetCertificateThumbprint(certificate)))
                        continue;

                    this.PutCertificateIntoMappings(certificate);
                }
                catch (Exception)
                {
                    // TODO: Log potentially corrupted certificate files in the peercerts folder
                }
            }
        }

        private void PutCertificateIntoMappings(X509Certificate certificate)
        {
            // TODO: Should the mappings also include the particular role of the certificate holder?
            this.mapThumbprints.TryAdd(MembershipServicesDirectory.GetCertificateThumbprint(certificate), certificate);
            this.mapCommonNames.TryAdd(MembershipServicesDirectory.GetCertificateCommonName(certificate), certificate);
            this.mapAddresses.TryAdd(MembershipServicesDirectory.GetCertificateTransactionSigningAddress(certificate, this.network), certificate);
            this.mapTransactionSigningPubKeyHash.TryAdd(MembershipServicesDirectory.ExtractCertificateExtension(certificate, CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid), certificate);
        }

        public string GetCertificatePath(MemberType memberType)
        {
            string subFolder;

            switch (memberType)
            {
                case MemberType.Admin:
                    subFolder = AdminCerts;
                    break;
                case MemberType.Revocation:
                    subFolder = Crls;
                    break;
                case MemberType.NetworkPeer:
                    subFolder = PeerCerts;
                    break;
                case MemberType.IntermediateCA:
                    subFolder = IntermediateCerts;
                    break;
                case MemberType.RootCA:
                    subFolder = CaCerts;
                    break;
                case MemberType.SelfSign:
                    subFolder = SignCerts;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(memberType), memberType, null);
            }

            return Path.Combine(this.rootFolder, subFolder);
        }

        public string GetCertificatePath(MemberType memberType, X509Certificate certificate)
        {
            var folder = GetCertificatePath(memberType);
            return Path.Combine(folder, $"{MembershipServicesDirectory.GetCertificateThumbprint(certificate)}");
        }
    }
}
