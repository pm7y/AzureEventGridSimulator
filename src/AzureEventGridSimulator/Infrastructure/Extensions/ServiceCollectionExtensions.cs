using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSimulatorSettings(this IServiceCollection services)
    {
        services.AddSingleton(
            sp =>
            {
                var settings = new SimulatorSettings();
                var configuration = sp.GetRequiredService<IConfiguration>();
                configuration.Bind(settings);
                settings.Validate();
                return settings;
            });

        return services;
    }
}
