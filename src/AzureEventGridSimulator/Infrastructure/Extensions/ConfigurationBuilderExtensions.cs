using Serilog;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ConfigurationBuilderExtensions
{
    extension(IConfigurationBuilder builder)
    {
        public IConfigurationBuilder AddCustomSimulatorConfigFileIfSpecified(
            IConfiguration configuration
        )
        {
            var configFileOverridden = configuration["ConfigFile"];

            if (!string.IsNullOrWhiteSpace(configFileOverridden))
            {
                if (!File.Exists(configFileOverridden))
                {
                    throw new FileNotFoundException(
                        "The specified ConfigFile could not be found.",
                        configFileOverridden
                    );
                }

                builder.AddJsonFile(
                    Path.Combine(Directory.GetCurrentDirectory(), configFileOverridden),
                    false,
                    false
                );
                Log.Warning("Overriding settings with '{ConfigFile}'", configFileOverridden);
            }

            return builder;
        }

        public IConfigurationBuilder AddEnvironmentVariablesAndCommandLine(string[] args)
        {
            return builder
                .AddEnvironmentVariables("ASPNETCORE_")
                .AddEnvironmentVariables("AEGS_")
                .AddCommandLine(args);
        }
    }
}
