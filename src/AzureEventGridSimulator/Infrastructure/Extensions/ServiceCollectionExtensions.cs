using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSimulatorSettings(this IServiceCollection services, IConfiguration configuration)
        {
            var settings = new SimulatorSettings();
            configuration.Bind(settings);
            settings.Validate();
            services.AddSingleton(_ => settings);

            return services;
        }
    }
}
