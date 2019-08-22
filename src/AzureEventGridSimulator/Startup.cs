using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Builder;
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
            var settings = new SimulatorSettings();
            Configuration.Bind(settings);
            settings.Validate();
            services.AddSingleton(o => settings);

            services.AddMediatR(Assembly.GetExecutingAssembly());
            services.AddHttpClient();

            services.AddHostedService<SubscriptionValidationService>();
            services.AddSingleton(o => _loggerFactory.CreateLogger(nameof(AzureEventGridSimulator)));
            services.AddScoped<SasKeyValidator>();
            services.AddSingleton<ValidationIpAddress>();

            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<EventGridMiddleware>();
            app.UseMvc();
        }
    }

    public class ValidationIpAddress
    {
        private readonly string _ipAddress;

        public ValidationIpAddress()
        {
            var hostName = Dns.GetHostName();
            _ipAddress = Dns.GetHostAddresses(hostName).First(ip => ip.AddressFamily == AddressFamily.InterNetwork &&
                                                                    !IPAddress.IsLoopback(ip)).ToString();
        }

        public override string ToString()
        {
            return _ipAddress;
        }

        public static implicit operator string(ValidationIpAddress d) => d.ToString();
    }
}
