using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using FluentAssertions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Feature.PoA.Tokenless;

namespace CertificateAuthority.Tests.Common
{
    public static class CaTestHelper
    {
        public const string AdminPassword = "4815162342";
        public static string BaseAddress = "http://localhost:5050";
        public const string CaMnemonic = "young shoe immense usual faculty edge habit misery swarm tape viable toddler";
        public const string CaMnemonicPassword = "node";

        private static readonly Random random = new Random();

        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static CredentialsModel CreateAccount(IWebHost server, AccountAccessFlags access = AccountAccessFlags.BasicAccess, bool approve = true, List<string> permissions = null)
        {
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
                "dummyOrganization",
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

        public static IWebHostBuilder CreateWebHostBuilder([CallerMemberName] string callingMethod = null)
        {
            // Create a datafolder path for the CA settings to use
            string hash = Guid.NewGuid().ToString("N").Substring(0, 7);
            string numberedFolderName = string.Join(
                ".",
                new[] { hash }.Where(s => s != null));
            string dataFolderName = Path.Combine(Path.GetTempPath(), callingMethod, numberedFolderName);

            var settings = new Settings();
            settings.Initialize(new string[] { $"-datadir={dataFolderName}", $"-serverurls={BaseAddress}" });

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
