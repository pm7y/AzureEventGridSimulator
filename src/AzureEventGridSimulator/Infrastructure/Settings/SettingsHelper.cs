using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AzureEventGridSimulator.Infrastructure.Settings
{
    public static class SettingsHelper
    {
        public static SimulatorSettings GetSimulatorSettings(IHostingEnvironment env)
        {
            var configuration = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json", false, false)
                                .AddJsonFile($"appsettings.{env.EnvironmentName.ToLowerInvariant().Trim()}.json", true, false)
                                .AddEnvironmentVariables()
                                .Build();

            var settings = new SimulatorSettings();
            configuration.Bind(settings);

            return settings;
        }
    }
}
