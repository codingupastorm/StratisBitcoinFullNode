using System.Reflection;
using CertificateAuthority.Controllers;
using CertificateAuthority.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace CertificateAuthority.API
{
    public class Startup
    {
        private readonly string _swaggerRoutePrefix;
        private readonly string _controllerRoutePrefix;

        public readonly IConfiguration Configuration;

        public Startup(IConfiguration configuration, Settings settings)
        {
            this.Configuration = configuration;

            string serviceRoutePrefix = settings?.ServiceRoutePrefix ?? string.Empty;
            this._swaggerRoutePrefix = string.IsNullOrEmpty(serviceRoutePrefix) ? string.Empty : serviceRoutePrefix + "/";
            this._controllerRoutePrefix = serviceRoutePrefix;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(options => 
            {
                options.EnableEndpointRouting = true;
                options.UseCentralRoutePrefix(new RouteAttribute(this._controllerRoutePrefix));
            })  
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddApplicationPart(typeof(AccountsController).GetTypeInfo().Assembly)
                .AddApplicationPart(typeof(CertificatesController).GetTypeInfo().Assembly)
                .AddApplicationPart(typeof(HelpersController).GetTypeInfo().Assembly);

            services.AddSwaggerGen(setupActions =>
            {
                setupActions.SwaggerDoc("v1", new OpenApiInfo { Title = "Stratis Certificate Authority API V1", Version = "v1" });

                setupActions.DocumentFilter<PathPrefixInsertDocumentFilter>(this._controllerRoutePrefix.Trim('/'));
            });

            services.AddSingleton<DataCacheLayer>();
            services.AddSingleton<CaCertificatesManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.ApplicationServices.GetService<DataCacheLayer>().Initialize();
            app.ApplicationServices.GetService<CaCertificatesManager>().Initialize();

            app.UseRouting();

            // Redirect root to swagger page
            var option = new RewriteOptions();
            option.AddRedirect("^$", $"/{this._swaggerRoutePrefix}swagger/index.html");
            app.UseRewriter(option);

            // Enable middleware to serve generated Swagger as a JSON endpoint.	            
            app.UseSwagger(c => { c.RouteTemplate = $"{this._swaggerRoutePrefix}swagger/{{documentName}}/swagger.json"; });

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c => { 
                c.SwaggerEndpoint($"/{this._swaggerRoutePrefix}swagger/v1/swagger.json", "Stratis Certificate Authority API V1");
                c.RoutePrefix = $"{this._swaggerRoutePrefix}swagger";
            });

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();

            app.UseHttpsRedirection();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}