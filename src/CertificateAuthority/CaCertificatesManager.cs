using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using NBitcoin;
using NBitcoin.DataEncoders;
using NLog;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Pkix;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.Utilities.Date;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Store;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace CertificateAuthority
{
    public sealed class CaCertificatesManager
    {
        private AsymmetricCipherKeyPair caKey;
        private X509Certificate caCertificate;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly DataCacheLayer repository;
        private readonly Settings settings;

        public const int CaAddressIndex = 0;
        public const string CaCertFilename = "CaCertificate.crt";
        public const string CaPfxFilename = "CaCertificate.pfx";

        /// <summary>
        /// Password to save / load the pfx with. We may want to move this to come from the command line?
        /// </summary>
        private const string CaPfxPassword = "5tr471s";

        public const string P2pkhExtensionOid = "1.4.1";
        public const string TransactionSigningPubKeyHashExtensionOid = "1.4.2";
        public const string BlockSigningPubKeyExtensionOid = "1.4.3";
        public const string SendPermission = "1.5.1";
        public const string CallContractPermissionOid = "1.5.2";
        public const string CreateContractPermissionOid = "1.5.3";

        public const int CertificateValidityPeriodYears = 10;
        public const int CaCertificateValidityPeriodYears = 10;

        public CaCertificatesManager(DataCacheLayer repository, Settings settings)
        {
            this.repository = repository;
            this.settings = settings;
        }

        /// <summary>
        /// Runs on server startup. Will load key and certificate from a pfx file if they exist.
        /// </summary>
        public void Initialize()
        {
            string caPfxPath = Path.Combine(this.settings.DataDirectory, CaPfxFilename);

            if (!File.Exists(caPfxPath))
            {
                // CA hasn't been initialised yet. Nothing to load. 
                return;
            }

            (X509Certificate certificate, AsymmetricKeyParameter key) = LoadPfx(File.ReadAllBytes(caPfxPath), CaPfxPassword);

            this.caCertificate = certificate;
            var ec = key as ECPrivateKeyParameters;
            ECPoint q = ec.Parameters.G.Multiply(ec.D);

            var pub = new ECPublicKeyParameters(q, ec.Parameters);
            // var pub = new ECPublicKeyParameters(ec.AlgorithmName, q, ec.PublicKeyParamSet);
            this.caKey = new AsymmetricCipherKeyPair(pub, ec); // TODO: This method of deriving pubkey from private could be made into its own method.
        }

        public bool InitializeCertificateAuthority(byte addressPrefix, string adminPassword, int coinType, string mnemonic, string mnemonicPassword)
        {
            if (this.caCertificate != null)
            {
                // CA is already initialized. Whoever is calling this probably shouldn't be.
                return false;
            }

            try
            {
                // TODO: Build the subject DN up as individual components to prevent reordering problems
                string caSubjectName = $"O={this.settings.CaSubjectNameOrganization},CN={this.settings.CaSubjectNameCommonName},OU={this.settings.CaSubjectNameOrganizationUnit}";
                string hdPath = $"m/44'/{coinType}'/0'/0/{CaAddressIndex}";

                var caAddressSpace = new HDWalletAddressSpace(mnemonic, mnemonicPassword);
                Key caPrivateKey = caAddressSpace.GetKey(hdPath).PrivateKey;
                
                // The CA is the big boss, and won't be signing transactions itself, so no extensions.
                var extensionData = new Dictionary<string, byte[]>();

                this.caKey = caAddressSpace.GetCertificateKeyPair(hdPath);
                this.caCertificate = CreateCertificateAuthorityCertificate(this.caKey, caSubjectName, null, null, extensionData);

                // Store the pfx file so that we can reload everything later on
                byte[] pfxFile = CreatePfx(this.caCertificate, caPrivateKey, CaPfxPassword);
                File.WriteAllBytes(Path.Combine(this.settings.DataDirectory, CaPfxFilename), pfxFile);

                // Many tests + tools are grabbing the certificate at this point. To keep that easily available we also store just the certificate.
                File.WriteAllBytes(Path.Combine(this.settings.DataDirectory, CaCertFilename), this.caCertificate.GetEncoded());

                SetAdminPassword(adminPassword);
            }
            catch (Exception ex)
            {
                this.caKey = null;
                this.caCertificate = null;

                this.logger.Error(ex.ToString());

                return false;
            }

            return true;
        }

        private void SetAdminPassword(string adminPassword)
        {
            this.repository.ChangeAccountPassword(Settings.AdminAccountId, adminPassword);
        }

        private static X509Certificate CreateCertificateAuthorityCertificate(AsymmetricCipherKeyPair subjectKeyPair, string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] usages, Dictionary<string, byte[]> extensionData)
        {
            SecureRandom random = GetSecureRandom();
            BigInteger subjectSerialNumber = GenerateSerialNumber(random);

            X509Certificate certificate = GenerateCertificate(random,
                subjectName, subjectKeyPair.Public, subjectSerialNumber, subjectAlternativeNames,
                subjectName, subjectKeyPair, subjectSerialNumber,
                true, usages, extensionData, CaCertificateValidityPeriodYears);

            return certificate;
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request file.
        /// </summary>
        public CertificateInfoModel IssueCertificate(CredentialsAccessWithModel<IssueCertificateFromRequestModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            if (model.Model.CertificateRequestFile.Length == 0)
                throw new Exception("Empty file!");

            var ms = new MemoryStream();

            model.Model.CertificateRequestFile.CopyTo(ms);

            // Convert to byte array
            var certRequest = new Pkcs10CertificationRequest(ms.ToArray());

            ms.Dispose();

            return this.IssueCertificate(certRequest, creator.Id);
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request base64 string.
        /// </summary>
        public CertificateInfoModel IssueCertificate(CredentialsAccessWithModel<IssueCertificateFromFileContentsModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            this.logger.Info("Issuing certificate from the following request: '{0}'.", model.Model.CertificateRequestFileContents);

            byte[] requestRaw = Convert.FromBase64String(model.Model.CertificateRequestFileContents);

            var certRequest = new Pkcs10CertificationRequest(requestRaw);

            return this.IssueCertificate(certRequest, creator.Id);
        }

        private CertificateInfoModel IssueCertificate(Pkcs10CertificationRequest certRequest, int creatorId)
        {
            X509Certificate certificateFromReq = IssueCertificateFromRequest(certRequest, this.caCertificate, this.caKey, new string[0], new[] { KeyPurposeID.AnyExtendedKeyUsage });
            Asn1Set attributes = certRequest.GetCertificationRequestInfo().Attributes;

            // TODO: This should come from the transaction signing pubkeyhash. Presently the address could be different to the pubkey hash, which is nonsensical.
            // The address thus could be stored in the db but doesn't need to be its own field in the certificate.
            string p2pkh = Encoding.UTF8.GetString(ExtractExtensionFromCsr(attributes, P2pkhExtensionOid));

            var transactionSigningPubKeyHashBytes = ExtractExtensionFromCsr(attributes, TransactionSigningPubKeyHashExtensionOid);
            byte[] blockSigningPubKeyBytes = ExtractExtensionFromCsr(attributes, BlockSigningPubKeyExtensionOid);

            X509Certificate2 tempCert = ConvertCertificate(certificateFromReq, GetSecureRandom());

            var infoModel = new CertificateInfoModel()
            {
                Status = CertificateStatus.Good,
                Thumbprint = tempCert.Thumbprint,
                Address = p2pkh,
                TransactionSigningPubKeyHash = transactionSigningPubKeyHashBytes,
                BlockSigningPubKey = blockSigningPubKeyBytes,
                CertificateContentDer = certificateFromReq.GetEncoded(),
                IssuerAccountId = creatorId
            };

            this.repository.AddNewCertificate(infoModel);

            // TODO: Include timestamp and possibly thumbprint to distinguish between multiple versions of the same certificate for a given address
            string certFilename = $"{p2pkh}.crt";

            File.WriteAllBytes(Path.Combine(this.settings.DataDirectory, certFilename), certificateFromReq.GetEncoded());

            this.logger.Info("New certificate was issued by account id {0}; certificate: '{1}'.", creatorId, infoModel);

            return infoModel;
        }

        private static X509Certificate IssueCertificateFromRequest(Pkcs10CertificationRequest certificateSigningRequest, X509Certificate issuerCertificate, AsymmetricCipherKeyPair issuerKeyPair, string[] subjectAlternativeNames, KeyPurposeID[] usages)
        {
            SecureRandom random = GetSecureRandom();
            BigInteger subjectSerialNumber = GenerateSerialNumber(random);

            CertificationRequestInfo certificationRequestInfo = certificateSigningRequest.GetCertificationRequestInfo();
            string subjectName = certificateSigningRequest.GetCertificationRequestInfo().Subject.ToString();
            AsymmetricKeyParameter publicKey = certificateSigningRequest.GetPublicKey();

            byte[] oid141 = ExtractExtensionFromCsr(certificationRequestInfo.Attributes, P2pkhExtensionOid);
            byte[] oid142 = ExtractExtensionFromCsr(certificationRequestInfo.Attributes, TransactionSigningPubKeyHashExtensionOid);
            byte[] oid144 = ExtractExtensionFromCsr(certificationRequestInfo.Attributes, BlockSigningPubKeyExtensionOid);

            var extensionData = new Dictionary<string, byte[]>
            {
                {P2pkhExtensionOid, oid141},
                {TransactionSigningPubKeyHashExtensionOid, oid142},
                {BlockSigningPubKeyExtensionOid, oid144}
            };

            X509Certificate certificate = GenerateCertificate(random,
                subjectName, publicKey, subjectSerialNumber, subjectAlternativeNames,
                issuerCertificate.SubjectDN.ToString(), issuerKeyPair, issuerCertificate.SerialNumber,
                false, usages, extensionData, CertificateValidityPeriodYears);

            return certificate;
        }

        private static byte[] ExtractExtensionFromCsr(Asn1Set csrAttributes, string oidToExtract)
        {
            // TODO: Surely BouncyCastle has a more direct way of extracting an extension from a CSR by OID?
            // http://unitstep.net/blog/2008/10/27/extracting-x509-extensions-from-a-csr-using-the-bouncy-castle-apis/
            // http://bouncy-castle.1462172.n4.nabble.com/Parsing-Certificate-and-CSR-Extension-Data-td3859749.html
            foreach (Asn1Encodable encodable in csrAttributes)
            {
                if (!(encodable is DerSequence sequence))
                    continue;

                if (!(sequence[0] is DerObjectIdentifier oid) || !(sequence[1] is DerSet set))
                    continue;

                if (oid.Id != PkcsObjectIdentifiers.Pkcs9AtExtensionRequest.Id)
                    continue;

                foreach (DerSequence seq1 in set)
                {
                    // TODO: Is the hierarchy for extensions always this depth or is it possible to 'flatten' it first to simplify lookup?
                    foreach (var item in seq1)
                    {
                        if (!(item is DerSequence itemSeq))
                            continue;

                        if (!(itemSeq[0] is DerObjectIdentifier oid1) || oid1.Id != oidToExtract)
                            continue;

                        // [0] = oid
                        // [1] = critical flag
                        // [2] = value
                        if (itemSeq[2] is DerOctetString octets)
                            return octets.GetOctets();
                    }
                }
            }

            return null;
        }

        private static SecureRandom GetSecureRandom()
        {
            // Since we're on Windows, we'll use the CryptoAPI one (on the assumption
            // that it might have access to better sources of entropy than the built-in
            // Bouncy Castle ones):
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);

            return random;
        }

        private static X509Certificate GenerateCertificate(SecureRandom random,
                                                           string subjectName,
                                                           AsymmetricKeyParameter subjectPublicKey,
                                                           BigInteger subjectSerialNumber,
                                                           string[] subjectAlternativeNames,
                                                           string issuerName,
                                                           AsymmetricCipherKeyPair issuerKeyPair,
                                                           BigInteger issuerSerialNumber,
                                                           bool isCertificateAuthority,
                                                           KeyPurposeID[] usages,
                                                           Dictionary<string, byte[]> extensionData,
                                                           int validityPeriodYears)
        {
            var certificateGenerator = new X509V3CertificateGenerator();
            certificateGenerator.SetSerialNumber(subjectSerialNumber);

            var issuerDN = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDN);

            // Note: The subject can be omitted if you specify a subject alternative name (SAN).
            var subjectDN = new X509Name(subjectName);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Our certificate needs valid from/to values.
            DateTime notBefore = DateTime.UtcNow.Date;
            DateTime notAfter = notBefore.AddYears(validityPeriodYears);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // The subject's public key goes in the certificate.
            certificateGenerator.SetPublicKey(subjectPublicKey);

            AddAuthorityKeyIdentifier(certificateGenerator, issuerDN, issuerKeyPair, issuerSerialNumber);
            AddSubjectKeyIdentifier(certificateGenerator, subjectPublicKey);
            AddBasicConstraints(certificateGenerator, isCertificateAuthority);

            if (usages != null && usages.Any())
                AddExtendedKeyUsage(certificateGenerator, usages);

            if (subjectAlternativeNames != null && subjectAlternativeNames.Any())
                AddSubjectAlternativeNames(certificateGenerator, subjectAlternativeNames);

            AddDltInformation(certificateGenerator, extensionData);

            // The certificate is signed with the issuer's private key.
            ISignatureFactory signatureFactory = new Asn1SignatureFactory("SHA256WithECDSA", issuerKeyPair.Private, random);
            X509Certificate certificate = certificateGenerator.Generate(signatureFactory);

            return certificate;
        }

        /// <summary>
        /// The certificate needs a serial number. This is used for revocation,
        /// and usually should be an incrementing index (which makes it easier to revoke a range of certificates).
        /// Since we don't have anywhere to store the incrementing index, we can just use a random number.
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        private static BigInteger GenerateSerialNumber(SecureRandom random)
        {
            BigInteger serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);

            return serialNumber;
        }

        /// <summary>
        /// Add the Authority Key Identifier. According to http://www.alvestrand.no/objectid/2.5.29.35.html, this
        /// identifies the public key to be used to verify the signature on this certificate.
        /// In a certificate chain, this corresponds to the "Subject Key Identifier" on the *issuer* certificate.
        /// The Bouncy Castle documentation, at http://www.bouncycastle.org/wiki/display/JA1/X.509+Public+Key+Certificate+and+Certification+Request+Generation,
        /// shows how to create this from the issuing certificate. Since we're creating a self-signed certificate, we have to do this slightly differently.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="issuerDN"></param>
        /// <param name="issuerKeyPair"></param>
        /// <param name="issuerSerialNumber"></param>
        private static void AddAuthorityKeyIdentifier(X509V3CertificateGenerator certificateGenerator, X509Name issuerDN, AsymmetricCipherKeyPair issuerKeyPair, BigInteger issuerSerialNumber)
        {
            SubjectPublicKeyInfo subjectPublicKeyInfo = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(issuerKeyPair.Public);
            var generalNames = new GeneralNames(new GeneralName(issuerDN));
            var authorityKeyIdentifierExtension = new AuthorityKeyIdentifier(subjectPublicKeyInfo, generalNames, issuerSerialNumber);
            certificateGenerator.AddExtension(X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifierExtension);
        }

        /// <summary>
        /// Add the "Subject Alternative Names" extension. Note that you have to repeat
        /// the value from the "Subject Name" property.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="subjectAlternativeNames"></param>
        private static void AddSubjectAlternativeNames(X509V3CertificateGenerator certificateGenerator, IEnumerable<string> subjectAlternativeNames)
        {
            Asn1Encodable[] altnames = subjectAlternativeNames.Select(name => new GeneralName(GeneralName.DnsName, name)).ToArray<Asn1Encodable>();
            var subjectAlternativeNamesExtension = new DerSequence(altnames);
            certificateGenerator.AddExtension(X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);
        }

        /// <summary>
        /// Add the "Extended Key Usage" extension, specifying (for example) "server authentication".
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="usages"></param>
        private static void AddExtendedKeyUsage(X509V3CertificateGenerator certificateGenerator, KeyPurposeID[] usages)
        {
            certificateGenerator.AddExtension(X509Extensions.ExtendedKeyUsage.Id, false, new ExtendedKeyUsage(usages));
        }

        /// <summary>
        /// Add the "Basic Constraints" extension.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="isCertificateAuthority"></param>
        private static void AddBasicConstraints(X509V3CertificateGenerator certificateGenerator, bool isCertificateAuthority)
        {
            certificateGenerator.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(isCertificateAuthority));
        }

        /// <summary>
        /// Add the Subject Key Identifier.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="subjectPublicKey"></param>
        private static void AddSubjectKeyIdentifier(X509V3CertificateGenerator certificateGenerator, AsymmetricKeyParameter subjectPublicKey)
        {
            var subjectKeyIdentifierExtension = new SubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(subjectPublicKey));
            certificateGenerator.AddExtension(X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifierExtension);
        }

        private static void AddDltInformation(X509V3CertificateGenerator certificateGenerator, Dictionary<string, byte[]> extensionData)
        {
            if (extensionData.TryGetValue(P2pkhExtensionOid, out byte[] oid141))
                certificateGenerator.AddExtension(P2pkhExtensionOid, false, new DerOctetString(oid141));

            if (extensionData.TryGetValue(TransactionSigningPubKeyHashExtensionOid, out byte[] oid142))
                certificateGenerator.AddExtension(TransactionSigningPubKeyHashExtensionOid, false, new DerOctetString(oid142));

            // TODO: Should this be explicitly requested in the CSR?
            certificateGenerator.AddExtension(SendPermission, false, new byte[] { 1 });

            if (extensionData.TryGetValue(BlockSigningPubKeyExtensionOid, out byte[] oid143))
                certificateGenerator.AddExtension(BlockSigningPubKeyExtensionOid, false, new DerOctetString(oid143));
            certificateGenerator.AddExtension(CallContractPermissionOid, false, new byte[] { 1 });
            certificateGenerator.AddExtension(CreateContractPermissionOid, false, new byte[] { 1 });
        }

        public static X509Certificate2 ConvertCertificate(X509Certificate certificate, SecureRandom random)
        {
            // Now to convert the Bouncy Castle certificate to a .NET certificate.
            // See http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
            // ...but, basically, we create a PKCS12 store (a .PFX file) in memory, and add the public and private key to that.
            var store = new Pkcs12Store();

            // What Bouncy Castle calls "alias" is the same as what Windows terms the "friendly name".
            string friendlyName = certificate.SubjectDN.ToString();

            // Add the certificate.
            var certificateEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(friendlyName, certificateEntry);

            X509Certificate2 convertedCertificate;
            using (var stream = new MemoryStream())
            {
                store.Save(stream, new char[0], random);
                convertedCertificate = new X509Certificate2(stream.ToArray());
            }

            return convertedCertificate;
        }

        public CertificateInfoModel GetCaCertificate(CredentialsAccessModel accessModelInfo)
        {
            if (this.caCertificate == null)
                return null;

            X509Certificate2 tempCert = ConvertCertificate(this.caCertificate, GetSecureRandom());

            return new CertificateInfoModel()
            {
                // TODO: Technically there is an address associated with the CA's pubkey, should we use it?
                Address = "",
                TransactionSigningPubKeyHash = null,
                CertificateContentDer = this.caCertificate.GetEncoded(),
                Id = 0,
                IssuerAccountId = 0,
                RevokerAccountId = 0,
                Status = CertificateStatus.Good,
                Thumbprint = tempCert.Thumbprint
            };
        }

        /// <summary>
        /// Provides collection of all issued certificates.
        /// </summary>
        public List<CertificateInfoModel> GetAllCertificates(CredentialsAccessModel accessModelInfo)
        {
            return this.repository.ExecuteQuery(accessModelInfo, (dbContext) => { return dbContext.Certificates.ToList(); });
        }

        /// <summary>
        /// Provides the collection of public keys of all non-revoked certificates.
        /// </summary>
        public List<string> GetCertificatePublicKeys()
        {
            return this.repository.PublicKeys.ToList();
        }

        /// <summary>
        /// Finds issued certificate by thumbprint and returns it or null if it wasn't found.
        /// </summary>
        public CertificateInfoModel GetCertificateByThumbprint(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            return this.repository.ExecuteQuery(model, (dbContext) => { return dbContext.Certificates.SingleOrDefault(x => x.Thumbprint == model.Model.Thumbprint); });
        }

        /// <summary>
        /// Finds issued certificate by pubkey hash and returns it or null if it wasn't found.
        /// </summary>
        public CertificateInfoModel GetCertificateByTransactionSigningPubKeyHash(CredentialsAccessWithModel<CredentialsModelWithPubKeyHashModel> model)
        {
            byte[] transactionSigningPubKeyHash = Convert.FromBase64String(model.Model.PubKeyHash);
            var byteArrayComparer = new ByteArrayComparer();

            return this.repository.ExecuteQuery(model, (dbContext) => { return dbContext.Certificates.SingleOrDefault(x => byteArrayComparer.Equals(x.TransactionSigningPubKeyHash, transactionSigningPubKeyHash)); });
        }

        /// <summary>
        /// Gets the status of a certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        public CertificateStatus GetCertificateStatusByThumbprint(string thumbprint)
        {
            if (this.repository.CertStatusesByThumbprint.TryGetValue(thumbprint, out CertificateStatus status))
                return status;

            return CertificateStatus.Unknown;
        }

        /// <summary>
        /// Returns all revoked certificates.
        /// </summary>
        public HashSet<string> GetRevokedCertificates()
        {
            return this.repository.RevokedCertificates;
        }

        /// <summary>
        /// Sets certificate status with the provided thumbprint to <see cref="CertificateStatus.Revoked"/>
        /// if certificate was found and it's status is <see cref="CertificateStatus.Good"/>.
        /// </summary>
        public bool RevokeCertificate(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            return this.repository.ExecuteCommand(model, (dbContext, account) =>
            {
                string thumbprint = model.Model.Thumbprint;

                if (this.GetCertificateStatusByThumbprint(thumbprint) != CertificateStatus.Good)
                    return false;

                this.repository.CertStatusesByThumbprint[thumbprint] = CertificateStatus.Revoked;

                CertificateInfoModel certToEdit = dbContext.Certificates.Single(x => x.Thumbprint == thumbprint);

                certToEdit.Status = CertificateStatus.Revoked;
                certToEdit.RevokerAccountId = account.Id;

                dbContext.Update(certToEdit);
                dbContext.SaveChanges();

                if (!dbContext.Certificates.Any(c => c.BlockSigningPubKey == certToEdit.BlockSigningPubKey && certToEdit.Status != CertificateStatus.Revoked))
                    this.repository.PublicKeys.Remove(Encoders.Hex.EncodeData(certToEdit.BlockSigningPubKey));

                this.repository.RevokedCertificates.Add(thumbprint);
                this.logger.Info("Certificate id {0}, thumbprint {1} was revoked.", certToEdit.Id, certToEdit.Thumbprint);

                return true;
            });
        }

        private static AttributePkcs GetAttributes(string[] subjectAlternativeNames, Dictionary<string, byte[]> extensionData)
        {
            IList oids = new ArrayList();
            IList values = new ArrayList();

            if (extensionData.TryGetValue(P2pkhExtensionOid, out byte[] oid141))
            {
                oids.Add(new DerObjectIdentifier(P2pkhExtensionOid));
                values.Add(new X509Extension(true, new DerOctetString(oid141)));
            }

            if (extensionData.TryGetValue(TransactionSigningPubKeyHashExtensionOid, out byte[] oid142))
            {
                oids.Add(new DerObjectIdentifier(TransactionSigningPubKeyHashExtensionOid));
                values.Add(new X509Extension(true, new DerOctetString(oid142)));
            }

            oids.Add(new DerObjectIdentifier(SendPermission));
            oids.Add(new DerObjectIdentifier(CallContractPermissionOid));
            oids.Add(new DerObjectIdentifier(CreateContractPermissionOid));

            values.Add(new X509Extension(true, new DerOctetString(new byte[] { 1 })));
            values.Add(new X509Extension(true, new DerOctetString(new byte[] { 1 })));
            values.Add(new X509Extension(true, new DerOctetString(new byte[] { 1 })));

            if (extensionData.TryGetValue(BlockSigningPubKeyExtensionOid, out byte[] oid144))
            {
                oids.Add(new DerObjectIdentifier(BlockSigningPubKeyExtensionOid));
                values.Add(new X509Extension(true, new DerOctetString(oid144)));
            }

            oids.Add(new DerObjectIdentifier(X509Extensions.SubjectAlternativeName.Id));

            Asn1Encodable[] altnames = subjectAlternativeNames.Select(name => new GeneralName(GeneralName.DnsName, name)).ToArray<Asn1Encodable>();
            var subjectAlternativeNamesExtension = new DerSequence(altnames);
            values.Add(new X509Extension(true, new DerOctetString(subjectAlternativeNamesExtension)));

            return new AttributePkcs(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(new X509Extensions(oids, values)));
        }

        public static Pkcs10CertificationRequestDelaySigned CreatedUnsignedCertificateSigningRequest(string subjectName, AsymmetricKeyParameter publicKey, string[] subjectAlternativeNames, Dictionary<string, byte[]> extensionData)
        {
            AttributePkcs attribute = GetAttributes(subjectAlternativeNames, extensionData);

            // Generate a certificate signing request without a signature
            var certificateRequest = new Pkcs10CertificationRequestDelaySigned(
                "SHA256withECDSA",
                // TODO: Build the subject DN up as individual components to prevent reordering problems
                new X509Name(subjectName),
                publicKey,
                new DerSet(attribute)
            );

            return certificateRequest;
        }

        public static string SignCertificateSigningRequest(string base64csr, Key privateKey, string ecdsaCurveFriendlyName = "secp256k1")
        {
            byte[] csrTemp = Convert.FromBase64String(base64csr);

            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);
            var privateKeyScalar = new BigInteger(1, privateKey.GetBytes());

            X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName(ecdsaCurveFriendlyName);
            var ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());
            var privateKeyParameter = new ECPrivateKeyParameters(privateKeyScalar, ecdsaDomainParams);

            byte[] signature = GenerateCSRSignature(unsignedCsr.GetDataToSign(), "SHA256withECDSA", privateKeyParameter);
            unsignedCsr.SignRequest(signature);

            var signedCsr = new Pkcs10CertificationRequest(unsignedCsr.GetDerEncoded());

            return Convert.ToBase64String(signedCsr.GetDerEncoded());
        }

        public static Pkcs10CertificationRequest CreateCertificateSigningRequest(string subjectName, AsymmetricCipherKeyPair subjectKeyPair, string[] subjectAlternativeNames, Dictionary<string, byte[]> extensionData)
        {
            AttributePkcs attribute = GetAttributes(subjectAlternativeNames, extensionData);

            // Generate a certificate signing request
            var certificateRequest = new Pkcs10CertificationRequest(
                "SHA256withECDSA",
                // TODO: Build the subject DN up as individual components to prevent reordering problems
                new X509Name(subjectName),
                subjectKeyPair.Public,
                new DerSet(attribute),
                subjectKeyPair.Private);

            return certificateRequest;
        }

        public static byte[] CreatePfx(X509Certificate certificate, Key privateKey, string password, string ecdsaCurveFriendlyName = "secp256k1")
        {
            var privateKeyScalar = new BigInteger(1, privateKey.GetBytes());

            X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName(ecdsaCurveFriendlyName);
            var ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());
            var privateKeyParameter = new ECPrivateKeyParameters(privateKeyScalar, ecdsaDomainParams);

            var builder = new Pkcs12StoreBuilder();
            builder.SetUseDerEncoding(true);
            Pkcs12Store store = builder.Build();

            var certEntry = new X509CertificateEntry(certificate);

            // The key entry's alias is not actually used directly for retrieval when loading the pfx, we search using a form of filter instead.
            store.SetKeyEntry("privateKey", new AsymmetricKeyEntry(privateKeyParameter), new X509CertificateEntry[] { certEntry });

            byte[] pfxBytes;

            using (var stream = new MemoryStream())
            {
                store.Save(stream, password.ToCharArray(), new SecureRandom());
                pfxBytes = stream.ToArray();
            }

            var result = Pkcs12Utilities.ConvertToDefiniteLength(pfxBytes);

            return result;
        }

        public static (X509Certificate, AsymmetricKeyParameter) LoadPfx(byte[] pfxData, string password)
        {
            var builder = new Pkcs12StoreBuilder();
            builder.SetUseDerEncoding(true);
            Pkcs12Store store = builder.Build();

            var stream = new MemoryStream(pfxData, false);
            store.Load(stream, password.ToCharArray());

            // Strictly speaking we know which alias we use to store the key, but as there should only be one entry in the store that IsKeyEntry will return for, search like this instead.
            string pName = null;
            foreach (string alias in store.Aliases)
            {
                if (!store.IsKeyEntry(alias))
                    continue;

                pName = alias;
                break;
            }

            AsymmetricKeyParameter privateKey = store.GetKey(pName).Key;
            X509CertificateEntry certEntry = store.GetCertificate(pName);

            return (certEntry.Certificate, privateKey);
        }

        public static bool ValidateCertificateChain(X509Certificate rootCert, X509Certificate clientCert)
        {
            try
            {
                var builder = new PkixCertPathBuilder();

                var rootCerts = new HashSet
                {
                    new TrustAnchor(rootCert, null)
                };

                var certStore = new List<X509Certificate>
                {
                    rootCert,
                    clientCert
                };

                var selector = new X509CertStoreSelector
                {
                    Subject = clientCert.SubjectDN,
                    IgnoreX509NameOrdering = true
                };

                var builderParams = new PkixBuilderParameters(rootCerts, selector)
                {
                    IsRevocationEnabled = false
                };

                var certStoreParameters = new X509CollectionStoreParameters(certStore);

                builderParams.AddStore(X509StoreFactory.Create("Certificate/Collection", certStoreParameters));
                builderParams.Date = new DateTimeObject(DateTime.Now);
                PkixCertPathBuilderResult result = builder.Build(builderParams);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        #region Utility methods for delayed-signing of CSRs
        public static byte[] GenerateCSRSignature(byte[] data, string signerAlgorithm, AsymmetricKeyParameter privateSigningKey)
        {
            ISigner signer = SignerUtilities.GetSigner(signerAlgorithm);
            signer.Init(true, privateSigningKey);
            signer.BlockUpdate(data, 0, data.Length);
            byte[] signature = signer.GenerateSignature();

            return signature;
        }
        #endregion
    }
}
