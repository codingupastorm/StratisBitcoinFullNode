using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

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

        private Random random;

        private readonly DataCacheLayer cache;

        private readonly Settings settings;

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public CertificatesManager(DataCacheLayer cache, Settings settings)
        {
            this.cache = cache;
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

            this.random = new Random();
        }

        /// <summary>Issues a new certificate using provided certificate request.</summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromRequestModel> model)
        {
            this.cache.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            if (model.Model.CertificateRequestFile.Length == 0)
                throw new Exception("Empty file!");

            int nextSerialNumber = cache.CertStatusesByThumbprint.Count;

            string requestFileName = $"certRequest_{random.Next(0, int.MaxValue).ToString()}.csr";
            var requestFullPath = Path.Combine(this.tempDirectory, requestFileName);

            using (var stream = new FileStream(requestFullPath, FileMode.Create))
                model.Model.CertificateRequestFile.CopyTo(stream);

            return await this.IssueCertificate(requestFullPath, nextSerialNumber, creator.Id);
        }

        /// <summary>Issues a new certificate using provided certificate request.</summary>
        public async Task<CertificateInfoModel> IssueCertificateAsync(CredentialsAccessWithModel<IssueCertificateFromFileContentsModel> model)
        {
            List<string> certificateRequestLines = DataHelper.GetCertificateRequestLines(model.Model.CertificateRequestFileContents);

            this.cache.VerifyCredentialsAndAccessLevel(model, out AccountModel creator);

            int nextSerialNumber = cache.CertStatusesByThumbprint.Count;

            string requestFileName = $"certRequest_{random.Next(0, int.MaxValue).ToString()}.csr";
            var requestFullPath = Path.Combine(this.tempDirectory, requestFileName);

            File.WriteAllLines(requestFullPath, certificateRequestLines);

            this.logger.Info("Issuing certificate from the following request: '{0}'.", model.Model.CertificateRequestFileContents);
            return await this.IssueCertificate(requestFullPath, nextSerialNumber, creator.Id);
        }

        /// <summary>Issues a new certificate using provided certificate request.</summary>
        private async Task<CertificateInfoModel> IssueCertificate(string certificateRequestFilePath, int nextSerialNumber, int creatorId)
        {
            int issueForDays = this.settings.DefaultIssuanceCertificateDays;

            string crtGeneratedPath = Path.Combine(this.certificatesDirectory, $"certificate_{nextSerialNumber.ToString()}.crt");
            string createCertCommand = $"\"{this.settings.OpenSslPath}\" x509 -req -days {issueForDays} -in \"{certificateRequestFilePath}\" -CA \"{certificatePath}\" -CAkey \"{certificateKeyPath}\" -set_serial {nextSerialNumber.ToString()} -out \"{crtGeneratedPath}\"";

            this.RunCmdCommand(createCertCommand);

            await this.WaitTillFileCreatedAsync(crtGeneratedPath, 2000);

            X509Certificate2 createdCertificate = new X509Certificate2(crtGeneratedPath);

            var infoModel = new CertificateInfoModel()
            {
                Status = CertificateStatus.Good,
                Thumbprint = createdCertificate.Thumbprint,
                CertificateContent = string.Join(" ", File.ReadAllLines(crtGeneratedPath)),
                IssuerAccountId = creatorId
            };

            cache.AddNewCertificate(infoModel);

            this.logger.Info("New certificate was issued by account id {0}; certificate: '{1}'.", creatorId, infoModel);

            File.Delete(certificateRequestFilePath);

            return infoModel;
        }

        /// <summary>Provides collection of all issued certificates.</summary>
        public List<CertificateInfoModel> GetAllCertificates(CredentialsAccessModel accessModelInfo)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.cache.VerifyCredentialsAndAccessLevel(accessModelInfo, dbContext, out AccountModel account);
                return dbContext.Certificates.ToList();
            }
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found.</summary>
        public CertificateInfoModel GetCertificateByThumbprint(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            using (CADbContext dbContext = this.CreateContext())
            {
                this.cache.VerifyCredentialsAndAccessLevel(model, dbContext, out AccountModel account);
                return dbContext.Certificates.SingleOrDefault(x => x.Thumbprint == model.Model.Thumbprint);
            }
        }

        /// <summary>
        /// Gets the status of a certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        public CertificateStatus GetCertificateStatusByThumbprint(string thumbprint)
        {
            if (this.cache.CertStatusesByThumbprint.TryGetValue(thumbprint, out CertificateStatus status))
                return status;

            return CertificateStatus.Unknown;
        }

        /// <summary> Returns all revoked certificates.</summary>
        public HashSet<string> GetRevokedCertificates()
        {
            return this.cache.RevokedCertificates;
        }

        /// <summary>
        /// Sets certificate status with the provided thumbprint to <see cref="CertificateStatus.Revoked"/>
        /// if certificate was found and it's status is <see cref="CertificateStatus.Good"/>.
        /// </summary>
        public bool RevokeCertificate(CredentialsAccessWithModel<CredentialsModelWithThumbprintModel> model)
        {
            string thumbprint = model.Model.Thumbprint;

            using (CADbContext dbContext = this.CreateContext())
            {
                this.cache.VerifyCredentialsAndAccessLevel(model, dbContext, out AccountModel caller);

                if (this.GetCertificateStatusByThumbprint(thumbprint) != CertificateStatus.Good)
                    return false;

                this.cache.CertStatusesByThumbprint[thumbprint] = CertificateStatus.Revoked;

                CertificateInfoModel certToEdit = dbContext.Certificates.Single(x => x.Thumbprint == thumbprint);

                certToEdit.Status = CertificateStatus.Revoked;
                certToEdit.RevokerAccountId = caller.Id;

                dbContext.Update(certToEdit);
                dbContext.SaveChanges();

                this.cache.RevokedCertificates.Add(thumbprint);
                this.logger.Info("Certificate id {0}, thumbprint {1} was revoked.", certToEdit.Id, certToEdit.Thumbprint);
            }

            return true;
        }

        private CADbContext CreateContext()
        {
            return new CADbContext(settings);
        }

        /// <summary>Executes command line command.</summary>
        private void RunCmdCommand(string arguments)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = "cmd.exe",
                Arguments = $"/c \"{arguments}\""
            };
            process.StartInfo = startInfo;
            process.Start();
        }

        private async Task WaitTillFileCreatedAsync(string path, int maxWaitMs)
        {
            int msWaited = 0;

            while (!File.Exists(path) && msWaited < maxWaitMs)
            {
                await Task.Delay(200);
                msWaited += 200;
            }
        }
    }
}
