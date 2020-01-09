using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CertificateAuthority.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var settings = new Settings();
            settings.Initialize(args);

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<Startup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });
            builder.Build().Run();
        }
    }
}
