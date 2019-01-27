using AzureEventGridSimulator.Middleware;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator
{
    public class Startup
    {
        private readonly ILoggerFactory _loggerFactory;

        public Startup(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            var settings = SettingsHelper.GetSimulatorSettings();

            services.AddScoped<ILogger>(o => _loggerFactory.CreateLogger(nameof(AzureEventGridSimulator)));
            services.AddScoped(o => settings);
            services.AddScoped<IAegSasHeaderValidator, SasKeyValidator>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAeg();
            app.UseAegHeaderValidation();
            app.UseAegEventValidation();
            app.UseAegTopicValidation();
            app.UseAegSizeValidation();

            app.UseMvc();
        }
    }
}
