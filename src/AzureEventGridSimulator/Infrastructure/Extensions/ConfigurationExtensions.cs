using Microsoft.Extensions.Configuration;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class ConfigurationExtensions
    {
        public static string EnvironmentName(this IConfiguration configuration)
        {
            return configuration["ENVIRONMENT"].Otherwise("Production");
        }
    }
}
