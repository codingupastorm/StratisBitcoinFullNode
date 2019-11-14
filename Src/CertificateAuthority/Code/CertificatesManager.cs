using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using NLog;

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

        private readonly NLog.Logger logger = LogManager.GetCurrentClassLogger();

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

            await this.WaitTillFIleCreatedAsync(crtGeneratedPath, 2000);

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

        /// <summary>Executes command line command.</summary>
        private void RunCmdCommand(string arguments)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c \"{arguments}\"";
            process.StartInfo = startInfo;
            process.Start();
        }

        private async Task WaitTillFIleCreatedAsync(string path, int maxWaitMs)
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
