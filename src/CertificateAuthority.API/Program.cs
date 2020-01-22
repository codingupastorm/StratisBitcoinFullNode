using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace CertificateAuthority.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var settings = new Settings();
            settings.Initialize(args);

            string logFolder = Path.Combine(settings.DataDirectory, "Logs");

            Directory.CreateDirectory(logFolder);

            var config = new NLog.Config.LoggingConfiguration();

            // Targets where to log to: File and Console
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = Path.Combine(logFolder, "node.txt") };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            // Rules for mapping loggers to targets            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);

            // Apply config           
            NLog.LogManager.Configuration = config;

            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<Startup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });
            builder.Build().Run();
        }
    }
}
