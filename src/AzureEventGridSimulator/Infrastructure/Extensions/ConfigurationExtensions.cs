namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ConfigurationExtensions
{
    public static string EnvironmentName(this IConfiguration configuration)
    {
        return (configuration["ENVIRONMENT"] ?? "Production").Otherwise("Production");
    }
}
