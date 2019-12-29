using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace CertificateAuthority.API
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

    public class Startup
    {
        private const string DataDirRoot = "StratisNode";
        private const string RootFolderName = "ca";
        private const string SubFolderName = "CaMain";
        private const string ConfigFileName = "ca.conf";

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddApplicationPart(typeof(AccountsController).GetTypeInfo().Assembly)
                .AddApplicationPart(typeof(CertificatesController).GetTypeInfo().Assembly)
                .AddApplicationPart(typeof(HelpersController).GetTypeInfo().Assembly);

            services.AddSwaggerGen(c =>
                c.SwaggerDoc("v1", new Info { Title = "Stratis Certificate Authority API V1", Version = "v1" }
                ));

            services.AddSingleton<Settings>();

            services.AddSingleton<DataCacheLayer>();
            services.AddSingleton<CaCertificatesManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            // Settings
            {
                string configurationFile = this.Configuration.GetValue<string>("conf", null)?.NormalizeDirectorySeparator();
                string dataDir = this.Configuration.GetValue<string>("datadir", null)?.NormalizeDirectorySeparator();
                string dataDirRoot = this.Configuration.GetValue<string>("datadirroot", DataDirRoot);

                if (dataDir != null && configurationFile != null)
                {
                    configurationFile = Path.Combine(dataDir, configurationFile);
                }

                // Set the full data directory path.
                if (dataDir == null)
                {
                    // Create the data directories if they don't exist.
                    dataDir = this.CreateDefaultDataDirectories(Path.Combine(dataDirRoot, RootFolderName), SubFolderName);
                }
                else
                {
                    // Combine the data directory with the default root folder and name.
                    string directoryPath = Path.Combine(dataDir, RootFolderName, SubFolderName);
                    dataDir = Directory.CreateDirectory(directoryPath).FullName;
                }

                if (configurationFile == null)
                    configurationFile = Path.Combine(dataDir, ConfigFileName);

                if (!File.Exists(configurationFile))
                    File.Create(configurationFile).Close();

                app.ApplicationServices.GetService<Settings>().Initialize(dataDir, configurationFile);
            }

            app.ApplicationServices.GetService<DataCacheLayer>().Initialize();
            app.ApplicationServices.GetService<CaCertificatesManager>().Initialize();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stratis Certificate Authority API V1"); });

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();

            app.UseHttpsRedirection();
            app.UseMvc();
        }

        private string CreateDefaultDataDirectories(string appName, string networkName)
        {
            string directoryPath;

            // Directory paths are different between Windows or Linux/OSX systems.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string home = Environment.GetEnvironmentVariable("HOME");

                if (!string.IsNullOrEmpty(home))
                    directoryPath = Path.Combine(home, "." + appName.ToLowerInvariant());
                else
                    throw new DirectoryNotFoundException("Could not find HOME directory.");
            }
            else
            {
                string localAppData = Environment.GetEnvironmentVariable("APPDATA");

                if (!string.IsNullOrEmpty(localAppData))
                    directoryPath = Path.Combine(localAppData, appName);
                else
                    throw new DirectoryNotFoundException("Could not find APPDATA directory.");
            }

            // Create the data directories if they don't exist.
            directoryPath = Path.Combine(directoryPath, networkName);
            Directory.CreateDirectory(directoryPath);

            return directoryPath;
        }
    }
}