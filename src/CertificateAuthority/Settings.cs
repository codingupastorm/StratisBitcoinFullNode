using System;
using System.IO;

namespace CertificateAuthority
{
    internal static class NormalizeDirectorySeparatorExt
    {
        /// <summary>
        /// Fixes incorrect directory separator characters in path (if any).
        /// </summary>
        public static string NormalizeDirectorySeparator(this string path)
        {
            // Replace incorrect with correct
            return path.Replace((Path.DirectorySeparatorChar == '/') ? '\\' : '/', Path.DirectorySeparatorChar);
        }
    }

    public class Settings
    {
        public const string AdminName = "Admin";

        public void Initialize(TextFileConfiguration textFileConfiguration)
        {
            configReader = textFileConfiguration;

            string dataDir = AppDomain.CurrentDomain.BaseDirectory.NormalizeDirectorySeparator();
            DataDirectory = configReader.GetOrDefault<string>("datadir", dataDir);

            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQLiteDatabase.db").NormalizeDirectorySeparator();
            DatabasePath = configReader.GetOrDefault<string>("dbpath", defaultPath);

            DefaultIssuanceCertificateDays = configReader.GetOrDefault<int>("defaultCertDays", 10 * 365);

            CreateAdminAccountOnCleanStart = configReader.GetOrDefault<bool>("createadmin", true);
            
            DefaultAdminPasswordHash = configReader.GetOrDefault<string>("adminpasshash", "6085fee2997a53fe15f195d907590238ec1f717adf6ac7fd4d7ed137f91892aa");
            
            CaSubjectNameOrganization = configReader.GetOrDefault<string>("caorganization", "Stratis");

            CaSubjectNameCommonName = configReader.GetOrDefault<string>("cacommonname", "DLT Root Certificate");

            CaSubjectNameOrganizationUnit = configReader.GetOrDefault<string>("caorganizationunit", "Administration");
        }

        private TextFileConfiguration configReader;

        public string DataDirectory { get; private set; }

        public string DatabasePath { get; private set; }

        public int DefaultIssuanceCertificateDays { get; private set; }

        public bool CreateAdminAccountOnCleanStart { get; private set; }

        public string DefaultAdminPasswordHash { get; private set; }

        public string CaSubjectNameOrganization { get; private set; }

        public string CaSubjectNameCommonName { get; private set; }

        public string CaSubjectNameOrganizationUnit { get; private set; }
    }
}
