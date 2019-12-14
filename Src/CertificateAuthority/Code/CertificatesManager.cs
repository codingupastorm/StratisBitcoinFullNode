using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using OpenSSL.Core;
using OpenSSL.Crypto;
using OpenSSL.X509;
using X509Certificate = OpenSSL.X509.X509Certificate;

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

        private X509CertificateAuthority certificateAuthority;

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
            
            string privateKeyPem = File.ReadAllText(this.certificateKeyPath);

            CryptoKey privateKey = CryptoKey.FromPrivateKey(privateKeyPem, "");

            var caCert = new X509Certificate(BIO.File(this.certificatePath, "r"));

            int nextSerialNumber = this.repository.CertStatusesByThumbprint.Count;

            this.certificateAuthority = new X509CertificateAuthority(caCert, privateKey, new SimpleSerialNumber(nextSerialNumber));
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request.
        /// </summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromRequestModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            if (model.Model.CertificateRequestFile.Length == 0)
                throw new Exception("Empty file!");

            var ms = new MemoryStream();

            model.Model.CertificateRequestFile.CopyTo(ms);

            BIO requestStream = BIO.MemoryBuffer();
            requestStream.Write(ms.ToArray());

            var certRequest = new X509Request(requestStream);

            requestStream.Dispose();
            ms.Dispose();

            return await this.IssueCertificate(certRequest, creator.Id);
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request.
        /// </summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromFileContentsModel> model)
        {
            this.repository.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);
            
            this.logger.Info("Issuing certificate from the following request: '{0}'.", model.Model.CertificateRequestFileContents);

            BIO requestStream = BIO.MemoryBuffer();
            requestStream.Write(string.Join("\n", DataHelper.GetCertificateRequestLines(model.Model.CertificateRequestFileContents)));

            var certRequest = new X509Request(requestStream);

            requestStream.Dispose();

            return await this.IssueCertificate(certRequest, creator.Id);
        }

        /// <summary>
        /// Issues a new certificate using provided certificate request.
        /// </summary>
        private async Task<CertificateInfoModel> IssueCertificate(X509Request certRequest, int creatorId)
        {
            int issueForDays = this.settings.DefaultIssuanceCertificateDays;

            DateTime start = DateTime.Today.AddDays(-1);
            DateTime end = start.AddDays(issueForDays);

            X509CertificateAuthority ca = this.GetCertificateAuthority();

            // TODO: Research whether it is necessary to supply a configuration
            Configuration cfg = null;
            string section = null;

            X509Certificate cert = ca.ProcessRequest(certRequest, start, end, cfg, section);

            // We need some data from the certificate that is not directly exposed by the OpenSSL version of the object.
            var createdCertificate = new X509Certificate2(cert.DER);

            var infoModel = new CertificateInfoModel()
            {
                Status = CertificateStatus.Good,
                Thumbprint = createdCertificate.Thumbprint,
                CertificateContent = cert.PEM,
                IssuerAccountId = creatorId
            };

            repository.AddNewCertificate(infoModel);

            this.logger.Info("New certificate was issued by account id {0}; certificate: '{1}'.", creatorId, infoModel);

            certRequest.Dispose();
            cfg?.Dispose();

            return infoModel;
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

        private X509CertificateAuthority GetCertificateAuthority()
        {
            return this.certificateAuthority;
        }
    }
}
