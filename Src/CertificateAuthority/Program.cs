﻿using CertificateAuthority.Code;
using CertificateAuthority.Code.Database;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.IO;
using System.Reflection;

namespace CertificateAuthority
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
            services.AddMvc(option => option.EnableEndpointRouting = false).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Stratis Certificate Authority API", Version = "v1" });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddSingleton<Settings>();

            services.AddSingleton<DataCacheLayer>();
            services.AddSingleton<CertificatesManager>();
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
            app.ApplicationServices.GetService<CertificatesManager>().Initialize();

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
