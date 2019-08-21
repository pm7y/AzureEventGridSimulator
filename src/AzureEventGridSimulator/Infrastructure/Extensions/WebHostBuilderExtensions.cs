using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions
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
