﻿using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CertificateAuthority.Tests.FullProjectTests.Helpers
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

    public static class StartupContainer
    {
        private static object locker = new object();

        private static List<TestOnlyStartup> startups = new List<TestOnlyStartup>();

        public static void RequestStartupCreation()
        {
            Task.Run(async () => await RequestStartupCreationAsync());
        }

        private static async Task RequestStartupCreationAsync()
        {
            await Task.Delay(1);
            Microsoft.AspNetCore.WebHost.CreateDefaultBuilder(new string[] { }).UseStartup<TestOnlyStartup>().Build().Run();
        }

        public static void StartupCreated(TestOnlyStartup instance)
        {
            lock (locker)
            {
                startups.Add(instance);
            }
        }

        public static TestOnlyStartup GetStartupWhenReady()
        {
            while (true)
            {
                Thread.Sleep(100);

                lock (locker)
                {
                    if (startups.Count > 0)
                    {
                        TestOnlyStartup item = startups.Last();
                        startups.Remove(item);
                        return item;
                    }
                }
            }
        }
    }

    public class TestOnlyStartup
    {
        public DataCacheLayer DataCacheLayer { get; private set; }

        public CaCertificatesManager CaCertificatesManager { get; private set; }

        public Settings Settings { get; private set; }

        public IConfiguration Configuration { get; }

        public TestOnlyStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddSingleton<Settings>();
            services.AddSingleton<DataCacheLayer>();
            services.AddSingleton<CaCertificatesManager>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            string testDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"test_only_db_{new Random().Next(0, int.MaxValue)}.db").NormalizeDirectorySeparator();
            string testConfigData = $"dbpath={testDbPath}";

            this.Settings = app.ApplicationServices.GetService<Settings>();
            this.Settings.Initialize(new TextFileConfiguration(testConfigData));

            this.DataCacheLayer = app.ApplicationServices.GetService<DataCacheLayer>();
            this.DataCacheLayer.Initialize();

            this.CaCertificatesManager = app.ApplicationServices.GetService<CaCertificatesManager>();
            this.CaCertificatesManager.Initialize();

            app.UseDeveloperExceptionPage();
            app.UseHttpsRedirection();
            app.UseMvc();

            StartupContainer.StartupCreated(this);
        }

        public AccountsController CreateAccountsController()
        {
            return new AccountsController(this.DataCacheLayer);
        }

        public CertificatesController CreateCertificatesController()
        {
            return new CertificatesController(this.CaCertificatesManager);
        }
    }
}
