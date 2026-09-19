using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceProviderExtensions
{
    extension(IServiceProvider provider)
    {
        public SimulatorSettings SimulatorSettings()
        {
            return provider.GetRequiredService<SimulatorSettings>();
        }
    }
}
