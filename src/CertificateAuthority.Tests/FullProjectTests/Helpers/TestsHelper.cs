﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CertificateAuthority.Tests.FullProjectTests.Helpers
{
    public static class TestsHelper
    {
        public static string BaseAddress = "http://localhost:5050";
        private static Random random = new Random();

        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static CredentialsModel CreateAccount(AccountAccessFlags access = AccountAccessFlags.BasicAccess, CredentialsModel creatorCredentialsModel = null)
        {
            string password = GenerateRandomString();
            string passHash = DataHelper.ComputeSha256Hash(password);

            IWebHostBuilder builder = CreateWebHostBuilder();
            var server = new TestServer(builder);

            var adminCredentials = new CredentialsModel(1, "4815162342");

            var accountsController = (AccountsController)server.Host.Services.GetService(typeof(AccountsController));

            CredentialsModel credentialsModel = creatorCredentialsModel ?? adminCredentials;
            int id = accountsController.CreateAccount(new CreateAccount(GenerateRandomString(), passHash, (int)access, credentialsModel.AccountId, credentialsModel.Password)).Value;

            return new CredentialsModel(id, password);
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
    }
}
