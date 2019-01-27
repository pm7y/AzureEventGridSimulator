using AzureEventGridSimulator.Middleware;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AzureEventGridSimulator
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            var settings = SettingsHelper.GetSimulatorSettings();

            services.AddScoped<SimulatorSettings>(o => settings);
            services.AddScoped<ILogger>(o => Log.Logger);
            services.AddScoped<IAegSasHeaderValidator, SasKeyValidator>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseAegSasHeaderValidation();
            app.UseAegMessageValidation();

            app.UseMvc();
        }
    }
}
