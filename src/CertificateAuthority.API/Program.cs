using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Reflection;
using Swashbuckle.AspNetCore.Swagger;

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

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
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
                string configurationFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ca.conf");
                string configData = string.Empty;

                if (File.Exists(configurationFilePath))
                    configData = File.ReadAllText(configurationFilePath);
                else
                    File.Create(configurationFilePath);

                app.ApplicationServices.GetService<Settings>().Initialize(new TextFileConfiguration(configData));
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
    }
}
