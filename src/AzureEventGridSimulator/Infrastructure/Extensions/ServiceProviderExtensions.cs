using AzureEventGridSimulator.Infrastructure.Settings;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceProviderExtensions
{
    extension(IServiceProvider provider)
    {
        public SimulatorSettings SimulatorSettings()
        {
            return provider.GetService<SimulatorSettings>();
        }

        public IEnumerable<TopicSettings> EnabledTopics()
        {
            return SimulatorSettings(provider).Topics.Where(o => !o.Disabled);
        }
    }
}
