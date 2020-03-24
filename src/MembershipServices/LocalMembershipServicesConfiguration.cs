using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Utilities;

namespace MembershipServices
{
    public class LocalMembershipServicesConfiguration
    {
        // TODO: Share pieces of local and channel config from a base class? Are they going to be similar enough?

        public string baseDir { get; set; }

        /// <summary>
        /// Configures the supported Organizational Units and identity classifications.
        /// </summary>
        public const string ConfigurationFilename = "config.yaml";

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
        private readonly ConcurrentDictionary<byte[], X509Certificate> mapTransactionSigningPubKeyHash;
        private readonly HashSet<string> revokedCertificateThumbprints;

        public LocalMembershipServicesConfiguration(string baseDir)
        {
            // TODO: Use the identifier in the base path. Specify the local MSD ID on first startup?
            this.baseDir = baseDir;

            this.mapThumbprints = new ConcurrentDictionary<string, X509Certificate>();
            this.mapCommonNames = new ConcurrentDictionary<string, X509Certificate>();
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
            catch (Exception e)
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
                this.mapTransactionSigningPubKeyHash.TryRemove(MembershipServicesDirectory.GetTransactionSigningPubKeyHash(certificate), out _);
            }
            catch (Exception e)
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

            // TODO: Delete from the local MSD folders and mappings too?
        }

        public bool IsCertificateRevoked(string thumbprint)
        {
            return this.revokedCertificateThumbprints.Contains(thumbprint);
        }

        public void InitializeFolderStructure()
        {
            Directory.CreateDirectory(this.baseDir);
            Directory.CreateDirectory(Path.Combine(this.baseDir, AdminCerts));
            Directory.CreateDirectory(Path.Combine(this.baseDir, CaCerts));
            Directory.CreateDirectory(Path.Combine(this.baseDir, IntermediateCerts));
            
            Directory.CreateDirectory(Path.Combine(this.baseDir, Crls));

            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, Crls)))
            {
                try
                {
                    this.revokedCertificateThumbprints.Add(Path.GetFileName(fileName));
                }
                catch (Exception e)
                {
                    // TODO: Log potentially corrupted certificate files in the signcerts folder
                }
            }

            Directory.CreateDirectory(Path.Combine(this.baseDir, Keystore));
            Directory.CreateDirectory(Path.Combine(this.baseDir, SignCerts));
            
            // There will probably only be one certificate in this folder, but nevertheless, load it into the lookups.
            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, SignCerts)))
            {
                try
                {
                    var parser = new X509CertificateParser();

                    X509Certificate certificate = parser.ReadCertificate(File.ReadAllBytes(fileName));

                    this.PutCertificateIntoMappings(certificate);
                }
                catch (Exception e)
                {
                    // TODO: Log potentially corrupted certificate files in the signcerts folder
                }
            }

            Directory.CreateDirectory(Path.Combine(this.baseDir, PeerCerts));

            foreach (string fileName in Directory.GetFiles(Path.Combine(this.baseDir, PeerCerts)))
            {
                try
                {
                    var parser = new X509CertificateParser();

                    X509Certificate certificate = parser.ReadCertificate(File.ReadAllBytes(fileName));

                    this.PutCertificateIntoMappings(certificate);
                }
                catch (Exception e)
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
            this.mapTransactionSigningPubKeyHash.TryAdd(MembershipServicesDirectory.GetTransactionSigningPubKeyHash(certificate), certificate);
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
