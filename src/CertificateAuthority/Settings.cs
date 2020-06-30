using System;
using System.IO;
using System.Runtime.InteropServices;

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
        public const int AdminAccountId = 1;
        public const string AdminName = "Admin";
        public const string AdminPasswordUnInitialized = "0000000000000000000000000000000000000000000000000000000000000000";
        private const string DataDirRoot = "StratisNode";
        private const string RootFolderName = "ca";
        private const string SubFolderName = "CaMain";
        private const string ConfigFileName = "ca.conf";

        public const string KeyStorePasswordKey = "keystorepassword";

        private string configurationFile;

        public string CertificateDirectory { get; private set; }
        public string DataDirectory { get; private set; }

        public string DatabasePath { get; private set; }

        public int DefaultIssuanceCertificateDays { get; private set; }

        public bool CreateAdminAccountOnCleanStart { get; private set; }

        public string DefaultAdminPasswordHash { get; private set; }

        public string CaSubjectNameOrganization { get; private set; }

        public string CaSubjectNameCommonName { get; private set; }

        public string CaSubjectNameOrganizationUnit { get; private set; }

        public string ServerUrls { get; private set; }

        public void Initialize(string[] commandLineArgs)
        {
            var commandLineArgsConfiguration = new TextFileConfiguration(commandLineArgs ?? Array.Empty<string>());

            this.configurationFile = commandLineArgsConfiguration.GetOrDefault<string>("conf", null)?.NormalizeDirectorySeparator();
            Console.WriteLine($"{nameof(this.configurationFile)}: {this.configurationFile}");

            this.DataDirectory = commandLineArgsConfiguration.GetOrDefault<string>("datadir", null)?.NormalizeDirectorySeparator();
            Console.WriteLine($"{nameof(this.DataDirectory)}: {this.DataDirectory}");

            string dataDirRoot = commandLineArgsConfiguration.GetOrDefault<string>("datadirroot", DataDirRoot);
            Console.WriteLine($"{nameof(dataDirRoot)}: {dataDirRoot}");

            this.ServerUrls = commandLineArgsConfiguration.GetOrDefault<string>("serverurls", null);
            if (!string.IsNullOrEmpty(this.ServerUrls))
                Console.WriteLine($"{nameof(this.ServerUrls)} set to: {this.ServerUrls}");

            if (this.DataDirectory != null && this.configurationFile != null)
                this.configurationFile = Path.Combine(this.DataDirectory, this.configurationFile);

            // Set the full data directory path.
            if (this.DataDirectory == null)
            {
                // Create the data directories if they don't exist.
                this.DataDirectory = this.CreateDefaultDataDirectories(Path.Combine(dataDirRoot, RootFolderName), SubFolderName);
            }
            else
            {
                // Combine the data directory with the default root folder and name.
                string directoryPath = Path.Combine(this.DataDirectory, RootFolderName, SubFolderName);
                this.DataDirectory = Directory.CreateDirectory(directoryPath).FullName;
            }

            this.CertificateDirectory = Path.Combine(this.DataDirectory, "Certificates");
            if (!Directory.Exists(this.CertificateDirectory))
                Directory.CreateDirectory(this.CertificateDirectory);

            Console.WriteLine($"{nameof(this.CertificateDirectory)}: {this.CertificateDirectory}");

            if (this.configurationFile == null)
            {
                Console.WriteLine("Configuration file not specified, setting default.");
                this.configurationFile = Path.Combine(this.DataDirectory, ConfigFileName);
            }
            else
            {
                Console.WriteLine($"Configuration file specified, setting to {this.configurationFile}");
                this.configurationFile = Path.Combine(this.DataDirectory, this.configurationFile);
            }

            if (!File.Exists(this.configurationFile))
            {
                Console.WriteLine("Configuration file does not exist, creating...");
                File.Create(this.configurationFile).Close();
            }

            var configFileArgs = new TextFileConfiguration(File.ReadAllText(this.configurationFile));

            string defaultDbPath = Path.Combine(this.DataDirectory, "certificatedatabase.db");
            this.DatabasePath = configFileArgs.GetOrDefault<string>("dbpath", defaultDbPath).NormalizeDirectorySeparator();

            this.DefaultIssuanceCertificateDays = configFileArgs.GetOrDefault<int>("defaultcertdays", 10 * 365);

            this.CreateAdminAccountOnCleanStart = configFileArgs.GetOrDefault<bool>("createadmin", true);

            this.DefaultAdminPasswordHash = configFileArgs.GetOrDefault<string>("adminpasshash", AdminPasswordUnInitialized);

            this.CaSubjectNameOrganization = configFileArgs.GetOrDefault<string>("caorganization", "Stratis");

            this.CaSubjectNameCommonName = configFileArgs.GetOrDefault<string>("cacommonname", "DLT Root Certificate");

            this.CaSubjectNameOrganizationUnit = configFileArgs.GetOrDefault<string>("caorganizationunit", "Administration");

            // If serverUrls is not set from command line, check the .conf file.
            if (string.IsNullOrEmpty(this.ServerUrls))
            {
                this.ServerUrls = configFileArgs.GetOrDefault<string>("serverurls", "https://0.0.0.0:5001;http://0.0.0.0:5050");
                Console.WriteLine($"{nameof(this.ServerUrls)} set to: {this.ServerUrls}");
            }
        }

        private string CreateDefaultDataDirectories(string appName, string networkName)
        {
            string directoryPath;

            // Directory paths are different between Windows or Linux/OSX systems.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string home = Environment.GetEnvironmentVariable("HOME");

                if (!string.IsNullOrEmpty(home))
                    directoryPath = Path.Combine(home, "." + appName.ToLowerInvariant());
                else
                    throw new DirectoryNotFoundException("Could not find HOME directory.");
            }
            else
            {
                string localAppData = Environment.GetEnvironmentVariable("APPDATA");

                if (!string.IsNullOrEmpty(localAppData))
                    directoryPath = Path.Combine(localAppData, appName);
                else
                    throw new DirectoryNotFoundException("Could not find APPDATA directory.");
            }

            // Create the data directories if they don't exist.
            directoryPath = Path.Combine(directoryPath, networkName);
            Directory.CreateDirectory(directoryPath);

            return directoryPath;
        }
    }
}
