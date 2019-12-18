using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace CertificateAuthority.Code
{
    public class CertificatesManager
    {
        private const string CertificateFileName = "RootCertificate.crt";

        private const string CertificateKey = "RootCertificateKey.key";

        private string tempDirectory;

        private string certificatePath;

        private string certificateKeyPath;

        private string certificatesDirectory;
        
        private readonly DataCacheLayer repository;

        private readonly Settings settings;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private AsymmetricCipherKeyPair caKey;

        private X509Certificate2 caCertificate;

        public CertificatesManager(DataCacheLayer cache, Settings settings)
        {
            this.repository = cache;
            this.settings = settings;
        }

        public void Initialize()
        {
            this.certificatesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IssuedCertificates");
            Directory.CreateDirectory(this.certificatesDirectory);

            this.tempDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
            Directory.CreateDirectory(this.tempDirectory);

            this.certificatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CertificateFileName);
            this.certificateKeyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CertificateKey);
            
            byte[] caOid141 = { 0x05, 0x04, 0x03, 0x09, 0x0a };
            string caSubjectName = "O=Stratis, CN=DLT Root Certificate (ECDSA), OU=Administration";
            int caAddressIndex = 0;

            // TODO: API function to initialise the CA certificate by providing mnemonic
            HDWalletAddressSpace caAddressSpace = new HDWalletAddressSpace("edge habit misery swarm tape viable toddler young shoe immense usual faculty", "node");

            this.caKey = caAddressSpace.GetCertificateKeyPair($"m/44'/105'/0'/0/{caAddressIndex}");
            this.caCertificate = CreateCertificateAuthorityCertificate(this.caKey, caSubjectName, null, null, caOid141);
        }

        private static X509Certificate2 CreateCertificateAuthorityCertificate(AsymmetricCipherKeyPair subjectKeyPair, string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] usages, byte[] oid141)
        {
            SecureRandom random = GetSecureRandom();
            BigInteger subjectSerialNumber = GenerateSerialNumber(random);

            X509Certificate certificate = GenerateCertificate(random,
                subjectName, subjectKeyPair.Public, subjectSerialNumber, subjectAlternativeNames,
                subjectName, subjectKeyPair, subjectSerialNumber,
                true, usages, oid141);

            return ConvertCertificate(certificate, random);
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request file.
        /// </summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromRequestModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            if (model.Model.CertificateRequestFile.Length == 0)
                throw new Exception("Empty file!");

            var ms = new MemoryStream();

            model.Model.CertificateRequestFile.CopyTo(ms);

            // Convert to byte array
            var certRequest = new Pkcs10CertificationRequest(ms.ToArray());

            ms.Dispose();

            return await this.IssueCertificate(certRequest, creator.Id);
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request base64 string.
        /// </summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromFileContentsModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            this.logger.Info("Issuing certificate from the following request: '{0}'.", model.Model.CertificateRequestFileContents);

            byte[] requestRaw = System.Convert.FromBase64String(model.Model.CertificateRequestFileContents);

            var certRequest = new Pkcs10CertificationRequest(requestRaw);
            
            return await this.IssueCertificate(certRequest, creator.Id);
        }

        private async Task<CertificateInfoModel> IssueCertificate(Pkcs10CertificationRequest certRequest, int creatorId)
        {
            X509Certificate2 certificateFromReq = IssueCertificateFromRequest(certRequest, caCertificate, caKey, new string[0], new[] { KeyPurposeID.AnyExtendedKeyUsage });
            
            var infoModel = new CertificateInfoModel()
            {
                Status = CertificateStatus.Good,
                Thumbprint = certificateFromReq.Thumbprint,
                CertificateContent = DataHelper.ConvertToPEM(certificateFromReq),
                IssuerAccountId = creatorId
            };

            repository.AddNewCertificate(infoModel);

            this.logger.Info("New certificate was issued by account id {0}; certificate: '{1}'.", creatorId, infoModel);

            return infoModel;
        }

        private static X509Certificate2 IssueCertificateFromRequest(Pkcs10CertificationRequest certificateSigningRequest, X509Certificate2 issuerCertificate, AsymmetricCipherKeyPair issuerKeyPair, string[] subjectAlternativeNames, KeyPurposeID[] usages)
        {
            SecureRandom random = GetSecureRandom();
            BigInteger issuerSerialNumber = new BigInteger(issuerCertificate.GetSerialNumber());
            BigInteger subjectSerialNumber = GenerateSerialNumber(random);

            CertificationRequestInfo certificationRequestInfo = certificateSigningRequest.GetCertificationRequestInfo();
            string subjectName = certificateSigningRequest.GetCertificationRequestInfo().Subject.ToString();
            AsymmetricKeyParameter publicKey = certificateSigningRequest.GetPublicKey();

            byte[] oid141 = new byte[0];
            DerSet set = (DerSet)certificationRequestInfo.Attributes;

            /*
            foreach (AttributePkcs att in set)
            {
                foreach (X509Extensions extensions in att.AttrValues)
                {
                    X509Extension extension = extensions.GetExtension(new DerObjectIdentifier("1.4.1"));
                    oid141 = (extension).Value.GetOctets();
                }
            }
            */
            X509Certificate certificate = GenerateCertificate(random,
                subjectName, publicKey, subjectSerialNumber, subjectAlternativeNames,
                issuerCertificate.Subject, issuerKeyPair, issuerSerialNumber,
                false, usages, oid141);

            //ECPublicKeyParameters p = new ECPublicKeyParameters()
            //var subjectKeyPair = certificationRequestInfo.SubjectPublicKeyInfo.PublicKeyData.GetBytes();
            //var subjectAlternativeNames;

            /*
            X509Certificate certificate = GenerateCertificate(random,
                subjectName, subjectKeyPair, subjectSerialNumber, subjectAlternativeNames,
                issuerCertificate.Subject, issuerKeyPair, issuerSerialNumber,
                false, usages);
                */

            return ConvertCertificate(certificate, random);
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
                                                           byte[] oid141)
        {
            X509V3CertificateGenerator certificateGenerator = new X509V3CertificateGenerator();
            certificateGenerator.SetSerialNumber(subjectSerialNumber);

            certificateGenerator.SetSignatureAlgorithm("SHA256WithECDSA");

            X509Name issuerDN = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDN);

            // Note: The subject can be omitted if you specify a subject alternative name (SAN).
            var subjectDN = new X509Name(subjectName);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Our certificate needs valid from/to values.
            var notBefore = DateTime.UtcNow.Date;
            var notAfter = notBefore.AddYears(2);

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

            AddDltInformation(certificateGenerator, oid141);

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
            var serialNumber =
                BigIntegers.CreateRandomInRange(
                    BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);

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
            GeneralNames generalNames = new GeneralNames(new GeneralName(issuerDN));
            AuthorityKeyIdentifier authorityKeyIdentifierExtension = new AuthorityKeyIdentifier(subjectPublicKeyInfo, generalNames, issuerSerialNumber);
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
            DerSequence subjectAlternativeNamesExtension = new DerSequence(altnames);
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

        private static void AddDltInformation(X509V3CertificateGenerator certificateGenerator, byte[] oid141)
        {
            certificateGenerator.AddExtension("1.4.1", true, oid141);
        }

        private static X509Certificate2 ConvertCertificate(X509Certificate certificate, SecureRandom random)
        {
            // Now to convert the Bouncy Castle certificate to a .NET certificate.
            // See http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
            // ...but, basically, we create a PKCS12 store (a .PFX file) in memory, and add the public and private key to that.
            Pkcs12Store store = new Pkcs12Store();

            // What Bouncy Castle calls "alias" is the same as what Windows terms the "friendly name".
            string friendlyName = certificate.SubjectDN.ToString();

            // Add the certificate.
            X509CertificateEntry certificateEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(friendlyName, certificateEntry);

            X509Certificate2 convertedCertificate;
            using (var stream = new MemoryStream())
            {
                store.Save(stream, new char[0], random);
                convertedCertificate = new X509Certificate2(stream.ToArray());
            }

            return convertedCertificate;
        }

        /// <summary>
        /// Provides collection of all issued certificates.
        /// </summary>
        public List<CertificateInfoModel> GetAllCertificates(CredentialsAccessModel accessModelInfo)
        {
            return this.repository.ExecuteQuery(accessModelInfo, (dbContext) => { return dbContext.Certificates.ToList(); });
        }

        /// <summary>
        /// Finds issued certificate by thumbprint and returns it or null if it wasn't found.
        /// </summary>
        public CertificateInfoModel GetCertificateByThumbprint(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            return this.repository.ExecuteQuery(model, (dbContext) => { return dbContext.Certificates.SingleOrDefault(x => x.Thumbprint == model.Model.Thumbprint); });
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

                this.repository.RevokedCertificates.Add(thumbprint);
                this.logger.Info("Certificate id {0}, thumbprint {1} was revoked.", certToEdit.Id, certToEdit.Thumbprint);

                return true;
            });
        }

        public static Pkcs10CertificationRequest CreateCertificateSigningRequest(string subjectName, AsymmetricCipherKeyPair subjectKeyPair, string[] subjectAlternativeNames, byte[] oid141)
        {
            // TODO: Is it possible for the CA to generate the CSR without a signature, and the client then signs it?

            IList oids = new ArrayList();
            IList values = new ArrayList();

            oids.Add(new DerObjectIdentifier("1.4.1"));
            values.Add(new X509Extension(true, new DerOctetString(oid141)));

            oids.Add(new DerObjectIdentifier(X509Extensions.SubjectAlternativeName.Id));
            Asn1Encodable[] altnames = subjectAlternativeNames.Select(name => new GeneralName(GeneralName.DnsName, name)).ToArray<Asn1Encodable>();
            DerSequence subjectAlternativeNamesExtension = new DerSequence(altnames);
            values.Add(new X509Extension(true, new DerOctetString(subjectAlternativeNamesExtension)));

            AttributePkcs attribute = new AttributePkcs(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest,
                new DerSet(new X509Extensions(oids, values)));

            // Generate a certificate signing request
            Pkcs10CertificationRequest certificateRequest = new Pkcs10CertificationRequest(
                "SHA1withECDSA",
                new X509Name(subjectName),
                subjectKeyPair.Public,
                new DerSet(attribute),
                subjectKeyPair.Private);

            return certificateRequest;
        }
    }
}
