using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;
using NLog.Web;

namespace CertificateAuthority.API
{
    public class Program
    {
        private const string NLogConfigFileName = "NLog.config";

        public static void Main(string[] args)
        {
            var settings = new Settings();
            settings.Initialize(args);

            string logFolder = Path.Combine(settings.DataDirectory, "logs");

            Directory.CreateDirectory(logFolder);

            var nlogConfigPath = InitializeNLog(settings);

            IWebHostBuilder builder = WebHost
                .CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    logging.AddNLog(nlogConfigPath);
                })
            .UseNLog();

            builder.UseUrls(settings.ServerUrls);
            builder.UseStartup<Startup>();
            builder.ConfigureServices((services) => { services.AddSingleton(settings); });
            IWebHost webhost = builder.Build();

            ConfigureLoggerPath(logFolder);

            webhost.Run();
        }

        /// <summary>
        /// Creates an NLog.config in the node folder if it doesn't exist.
        /// </summary>
        /// <returns>The path to the NLog file.</returns>
        private static string InitializeNLog(Settings settings)
        {
            var nlogConfigPath = Path.Combine(settings.DataDirectory, NLogConfigFileName);

            // File already exists. This could be from an earlier run, or it could be a user-defined config.
            if (File.Exists(nlogConfigPath))
                return nlogConfigPath;

            // This will copy the file from the executable directory to the node data folder.
            File.Copy(NLogConfigFileName, nlogConfigPath);

            return nlogConfigPath;
        }

        /// <summary>
        /// If we use "debug*" targets, which are defined in "NLog.config", make sure they log into the correct log folder in data directory.
        /// </summary>
        /// <param name="logPath">The log path to log to.</param>
        private static void ConfigureLoggerPath(string logPath)
        {
            var debugTargets = LogManager.Configuration.AllTargets.Where(t => (t.Name != null) && t.Name.StartsWith("debug")).ToList();
            foreach (Target debugTarget in debugTargets)
            {
                FileTarget debugFileTarget = debugTarget is AsyncTargetWrapper ? (FileTarget)((debugTarget as AsyncTargetWrapper).WrappedTarget) : (FileTarget)debugTarget;
                string currentFile = debugFileTarget.FileName.Render(new LogEventInfo { TimeStamp = DateTime.UtcNow });
                debugFileTarget.FileName = Path.Combine(logPath, Path.GetFileName(currentFile));

                if (debugFileTarget.ArchiveFileName != null)
                {
                    string currentArchive = debugFileTarget.ArchiveFileName.Render(new LogEventInfo { TimeStamp = DateTime.UtcNow });
                    debugFileTarget.ArchiveFileName = Path.Combine(logPath, currentArchive);
                }
            }
        }
    }
}
