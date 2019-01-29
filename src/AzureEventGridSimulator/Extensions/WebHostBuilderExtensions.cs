using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Extensions
{
    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseSimulatorSettings(this IWebHostBuilder hostBuilder)
        {
            return hostBuilder
                .ConfigureServices(services =>
                {
                    var settings = SettingsHelper.GetSimulatorSettings();

                    settings.Validate();

                    services.AddSingleton(o => settings);
                });
        }
    }
}
