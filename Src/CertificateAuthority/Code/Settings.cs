using System;
using System.IO;
using NLog;

namespace CertificateAuthority.Code
{
    public class Settings
    {
        public const string AdminName = "Admin";

        public void Initialize(TextFileConfiguration textFileConfiguration)
        {
            configReader = textFileConfiguration;

            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "litedb.db");
            LiteDbPath = configReader.GetOrDefault<string>("dbpath", defaultPath);

            DefaultIssuanceCertificateDays = configReader.GetOrDefault<int>("defaultCertDays", 10 * 365);

            CreateAdminAccountOnCleanStart = configReader.GetOrDefault<bool>("createadmin", true);
            DefaultAdminPasswordHash = configReader.GetOrDefault<string>("adminpasshash", "6085fee2997a53fe15f195d907590238ec1f717adf6ac7fd4d7ed137f91892aa");

            OpenSslPath = configReader.GetOrDefault<string>("opensslpath", @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe");
        }

        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private TextFileConfiguration configReader;

        public string LiteDbPath { get; private set; }

        public int DefaultIssuanceCertificateDays { get; private set; }

        public bool CreateAdminAccountOnCleanStart { get; private set; }

        public string DefaultAdminPasswordHash { get; private set; }

        public string OpenSslPath { get; private set; }
    }
}
