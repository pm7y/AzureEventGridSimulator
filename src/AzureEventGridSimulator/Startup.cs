using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AzureEventGridSimulator
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var settings = new SimulatorSettings();
            _configuration.Bind(settings);
            settings.Validate();
            services.AddSingleton(_ => settings);

            services.AddMediatR(Assembly.GetExecutingAssembly());
            services.AddHttpClient();

            services.AddScoped<SasKeyValidator>();
            services.AddSingleton<ValidationIpAddress>();

            services.AddControllers(options =>
                    {
                        options.EnableEndpointRouting = false;
                    })
                    .AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true; })
                    .SetCompatibilityVersion(CompatibilityVersion.Latest);

            services.AddApiVersioning(config =>
            {
                config.DefaultApiVersion = new ApiVersion(DateTime.Parse(Constants.SupportedApiVersion, new ApiVersionFormatProvider()));
                config.AssumeDefaultVersionWhenUnspecified = true;
                config.ReportApiVersions = true;
            });
        }

        // ReSharper disable once UnusedMember.Global
        // ReSharper disable once CA1822
        public static void Configure(IApplicationBuilder app,
                              IHostApplicationLifetime lifetime,
                              ILogger<Startup> logger)
        {
            lifetime.ApplicationStarted.Register(async () => await Task.CompletedTask.ContinueWith(_ => OnApplicationStarted(app, lifetime, logger)));

            app.UseSerilogRequestLogging();
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<EventGridMiddleware>();
            app.UseMvc();
        }

        private static async Task OnApplicationStarted(IApplicationBuilder app, IHostApplicationLifetime lifetime, ILogger logger)
        {
            logger.LogInformation("It's Alive !");

            var simulatorSettings = (SimulatorSettings)app.ApplicationServices.GetService(typeof(SimulatorSettings));

            if (simulatorSettings is null)
            {
                logger.LogCritical("Settings are not found. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            if (!simulatorSettings.Topics.Any())
            {
                logger.LogCritical("There are no configured topics. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            if (simulatorSettings.Topics.All(o => o.Disabled))
            {
                logger.LogCritical("All of the configured topics are disabled. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            if (app.ApplicationServices.GetService(typeof(IMediator)) is IMediator mediator)
            {
                await mediator.Send(new ValidateAllSubscriptionsCommand());
            }
        }
    }
}
