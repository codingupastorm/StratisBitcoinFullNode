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
        public string baseDir { get; set; }

        private readonly Network network;

        /// <summary>
        /// Subfolder holding certificate files each corresponding to an administrator certificate.
        /// </summary>
        public const string AdminCerts = "admincerts";

        /// <summary>
        /// Subfolder holding certificate files each corresponding to a root CA's certificate.
        /// </summary>
        public const string CaCerts = "cacerts";

        /// <summary>
        /// Subfolder holding certificate files each corresponding to an intermediate CA's certificate.
        /// </summary>
        /// <remarks>Optional.</remarks>
        public const string IntermediateCerts = "intermediatecerts";

        /// <summary>
        /// Subfolder holding the considered CRLs (certificate revocation lists).
        /// </summary>
        /// <remarks>Optional.</remarks>
        public const string Crls = "crls";

        /// <summary>
        /// Subfolder holding a file with the node's signing key (private key).
        /// </summary>
        /// <remarks>Only ECC keys are supported.</remarks>
        public const string Keystore = "keystore";

        /// <summary>
        /// Subfolder holding a file with the node's X.509 certificate (public key).
        /// </summary>
        public const string SignCerts = "signcerts";

        /// <summary>
        /// Subfolder holding certificate files for the peers the node is aware of. This is primarily for validating transaction signing, as P2P certificates do not yet require a central registry.
        /// </summary>
        /// <remarks>This is somewhat a stopgap solution until channels are properly implemented, as channels could be established between peers of (potentially) different organisations.
        /// It is also so that transaction validation/endorsement can be correctly performed, as a transaction signature does not contain any certificate information. So the MSD
        /// will have to be responsible for mapping the sender address in a transaction to a certificate stored in this folder. Further research about exactly how HL does this is required.</remarks>
        public const string PeerCerts = "peercerts";

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

        public LocalMembershipServicesConfiguration(string baseDir, Network network)
        {
            // TODO: Use the identifier in the base path. Specify the local MSD ID on first startup?
            this.baseDir = baseDir;

            this.network = network;

            this.mapThumbprints = new ConcurrentDictionary<string, X509Certificate>();
            this.mapCommonNames = new ConcurrentDictionary<string, X509Certificate>();
            this.mapAddresses = new ConcurrentDictionary<string, X509Certificate>();
            this.mapTransactionSigningPubKeyHash = new ConcurrentDictionary<byte[], X509Certificate>(new ByteArrayComparer());
            this.revokedCertificateThumbprints = new HashSet<string>();
        }

        public bool AddCertificate(X509Certificate certificate, MemberType memberType)
        {
            string pathToCert = GetCertificatePath(certificate, memberType);

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
            string pathToCert = GetCertificatePath(certificate, memberType);

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
            FileStream file = File.Create(Path.Combine(this.baseDir, Crls, thumbprint));
            file.Flush();
            file.Dispose();

            X509Certificate certificate = this.GetCertificateByThumbprint(thumbprint);

            this.RemoveCertificate(certificate, MemberType.NetworkPeer);
        }

        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.revokedCertificateThumbprints.Contains(thumbprint);
        }

        public static void InitializeFolderStructure(string rootDir)
        {
            Directory.CreateDirectory(rootDir);

            Directory.CreateDirectory(Path.Combine(rootDir, AdminCerts));
            Directory.CreateDirectory(Path.Combine(rootDir, CaCerts));
            Directory.CreateDirectory(Path.Combine(rootDir, IntermediateCerts));
            Directory.CreateDirectory(Path.Combine(rootDir, Crls));
            Directory.CreateDirectory(Path.Combine(rootDir, Keystore));
            Directory.CreateDirectory(Path.Combine(rootDir, SignCerts));
            Directory.CreateDirectory(Path.Combine(rootDir, PeerCerts));
        }

        public void InitializeExistingCertificates()
        {
            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, Crls)))
            {
                this.revokedCertificateThumbprints.Add(Path.GetFileName(fileName));
            }

            // There will probably only be one certificate in this folder, but nevertheless, load it into the lookups.
            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, SignCerts)))
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

            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, PeerCerts)))
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

        private string GetCertificatePath(X509Certificate certificate, MemberType memberType)
        {
            string subFolder;

            switch (memberType)
            {
                case MemberType.NetworkPeer:
                    subFolder = PeerCerts;
                    break;
                case MemberType.Admin:
                    subFolder = AdminCerts;
                    break;
                case MemberType.IntermediateCA:
                    subFolder = IntermediateCerts;
                    break;
                case MemberType.RootCA:
                    subFolder = CaCerts;
                    break;
                case MemberType.Self:
                    subFolder = SignCerts;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(memberType), memberType, null);
            }

            return Path.Combine(this.baseDir, subFolder, $"{MembershipServicesDirectory.GetCertificateThumbprint(certificate)}");
        }
    }
}
