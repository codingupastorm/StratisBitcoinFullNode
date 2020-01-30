using System;
using System.IO;
using CertificateAuthority.API;
using CertificateAuthority.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CertificateAuthority.Tests
{
    public class TestOnlyStartup : Startup
    {
        public TestOnlyStartup(IConfiguration configuration) : base(configuration)
        {
            configuration["conf"] = "ca.conf";
            configuration["datadir"] = GetTestDirectoryPath(this);
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddTransient<AccountsController>();
            services.AddTransient<CertificatesController>();
            services.AddTransient<HelpersController>();
        }

        private string GetTestDirectoryPath(object caller, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            string randomString = CaTestHelper.GenerateRandomString(6);

            return Path.Combine(Path.GetTempPath(), caller.GetType().Name, callingMethod, timeStamp + randomString);
        }
    }
}
