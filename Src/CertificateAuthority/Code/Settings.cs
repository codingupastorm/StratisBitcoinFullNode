using System;
using System.IO;

namespace CertificateAuthority.Code
{
    public class Settings
    {
        public const string AdminName = "Admin";

        public void Initialize(TextFileConfiguration textFileConfiguration)
        {
            configReader = textFileConfiguration;

            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLiteDatabase.db");
            DatabasePath = configReader.GetOrDefault<string>("dbpath", defaultPath);

            DefaultIssuanceCertificateDays = configReader.GetOrDefault<int>("defaultCertDays", 10 * 365);

            CreateAdminAccountOnCleanStart = configReader.GetOrDefault<bool>("createadmin", true);
            
            DefaultAdminPasswordHash = configReader.GetOrDefault<string>("adminpasshash", "6085fee2997a53fe15f195d907590238ec1f717adf6ac7fd4d7ed137f91892aa");
            
            CaSubjectNameOrganization = configReader.GetOrDefault<string>("caorganization", "Stratis");

            CaSubjectNameCommonName = configReader.GetOrDefault<string>("cacommonname", "DLT Root Certificate");

            CaSubjectNameOrganizationUnit = configReader.GetOrDefault<string>("caorganizationunit", "Administration");
        }

        private TextFileConfiguration configReader;

        public string DatabasePath { get; private set; }

        public int DefaultIssuanceCertificateDays { get; private set; }

        public bool CreateAdminAccountOnCleanStart { get; private set; }

        public string DefaultAdminPasswordHash { get; private set; }

        public string CaSubjectNameOrganization { get; private set; }

        public string CaSubjectNameCommonName { get; private set; }

        public string CaSubjectNameOrganizationUnit { get; private set; }
    }
}
