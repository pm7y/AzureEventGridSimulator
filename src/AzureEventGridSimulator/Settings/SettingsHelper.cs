using Microsoft.Extensions.Configuration;

namespace AzureEventGridSimulator.Settings
{
    public static class SettingsHelper
    {
        public static SimulatorSettings GetSimulatorSettings()
        {
            var configuration = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json")
                                .Build();

            var settings = new SimulatorSettings();
            configuration.Bind(settings);

            return settings;
        }
    }
}
