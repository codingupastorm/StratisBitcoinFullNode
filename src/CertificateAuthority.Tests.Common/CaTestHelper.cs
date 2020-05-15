using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using FluentAssertions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Stratis.Feature.PoA.Tokenless.Networks;

namespace CertificateAuthority.Tests.Common
{
    public class CaTester
    {
        protected int caPort;
        protected string caBaseAddress => $"http://localhost:{this.caPort}";

        public CaTester()
        {
            this.caPort = 5000 + (new Random().Next(1000));
        }
    }

    public static class CaTestHelper
    {
        public const string AdminPassword = "4815162342";
        public static string BaseAddress = "http://localhost:5050";
        public const string CaMnemonic = "young shoe immense usual faculty edge habit misery swarm tape viable toddler";
        public const string CaMnemonicPassword = "node";
        public const string TestOrganisation = "dummyOrganization";

        private static readonly Random random = new Random();

        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static CredentialsModel CreateAccount(IWebHost server, AccountAccessFlags access = AccountAccessFlags.BasicAccess, bool approve = true, List<string> permissions = null, string organisation = null)
        {
            if (organisation == null)
            {
                organisation = TestOrganisation;
            }

            // Default to all permissions unless otherwise restricted.
            List<string> accountPermissions = permissions ?? CaCertificatesManager.ValidPermissions;

            string password = GenerateRandomString();
            string passHash = DataHelper.ComputeSha256Hash(password);

            var adminCredentials = new CredentialsModel(Settings.AdminAccountId, AdminPassword);

            var accountsController = (AccountsController)server.Services.GetService(typeof(AccountsController));

            int id = GetValue<int>(accountsController.CreateAccount(new CreateAccountModel(GenerateRandomString(),
                passHash,
                (int)access,
                "dummyOrganizationUnit",
                organisation,
                "dummyLocality",
                "dummyStateOrProvince",
                "dummyEmailAddress",
                "dummyCountry",
                accountPermissions)));

            if (approve)
            {
                accountsController.ApproveAccount(new CredentialsModelWithTargetId()
                {
                    TargetAccountId = id,
                    AccountId = adminCredentials.AccountId,
                    Password = adminCredentials.Password
                });
            }

            return new CredentialsModel(id, password);
        }

        public static void InitializeCa(TestServer server)
        {
            var network = new TokenlessNetwork();
            var certificatesController = (CertificatesController)server.Host.Services.GetService(typeof(CertificatesController));
            var model = new InitializeCertificateAuthorityModel(CaMnemonic, CaMnemonicPassword, network.Consensus.CoinType, AdminPassword);
            certificatesController.InitializeCertificateAuthority(model);
        }

        public static IWebHostBuilder CreateWebHostBuilder(string dataFolderName, string caBaseAddress = null)
        {
            // Initialize settings
            var settings = new Settings();
            settings.Initialize(new string[] { $"-datadir={dataFolderName}", $"-serverurls={caBaseAddress ?? BaseAddress}" });

            // Create the log folder
            string logFolder = Path.Combine(settings.DataDirectory, "Logs");
            Directory.CreateDirectory(logFolder);

            // Initialize logging for tests.
            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = Path.Combine(logFolder, "ca.txt") };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            NLog.LogManager.Configuration = config;

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<TestOnlyStartup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });

            return builder;
        }

        public static T GetValue<T>(IActionResult response)
        {
            response.Should().BeOfType<JsonResult>();
            var result = (JsonResult)response;
            return (T)result.Value;
        }
    }
}
