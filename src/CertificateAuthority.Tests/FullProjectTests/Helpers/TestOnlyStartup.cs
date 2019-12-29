using System.IO;
using CertificateAuthority.API;
using CertificateAuthority.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CertificateAuthority.Tests.FullProjectTests.Helpers
{
    public class TestOnlyStartup : Startup
    {
        public string DataDir { get; set; }

        public TestOnlyStartup(IConfiguration configuration) : base(configuration)
        {
            this.DataDir = GetTestDirectoryPath(this);

            configuration["conf"] = "ca.conf";
            configuration["datadir"] = this.DataDir;
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
            return Path.Combine(Path.GetTempPath(), caller.GetType().Name, callingMethod);
        }
    }
}
