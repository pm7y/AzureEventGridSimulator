using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSimulatorSettings(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = new SimulatorSettings();
        configuration.Bind(settings);
        settings.Validate();
        services.AddSingleton(_ => settings);

        return services;
    }
}
