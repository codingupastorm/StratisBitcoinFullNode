using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace CertificateAuthority.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder(args);
            builder.UseStartup<Startup>().Build().Run();
        }
    }
}
