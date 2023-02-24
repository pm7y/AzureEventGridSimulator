using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ConfigurationBuilderExtensions
{
    public static WebApplicationBuilder AddConfiguration(this WebApplicationBuilder builder, string[] args)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, false)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, false)
            .AddCustomSimulatorConfigFileIfSpecified(args)
            .AddEnvironmentVariablesAndCommandLine(args)
            .AddInMemoryCollection(
                                    new Dictionary<string, string>
                                    {
                                        ["AEGS_Serilog__Using__0"] = "Serilog.Sinks.Console",
                                        ["AEGS_Serilog__Using__1"] = "Serilog.Sinks.File",
                                        ["AEGS_Serilog__Using__2"] = "Serilog.Sinks.Seq"
                                    });

        return builder;
    }

    private static IConfigurationBuilder AddCustomSimulatorConfigFileIfSpecified(this IConfigurationBuilder builder, string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariablesAndCommandLine(args)
            .Build();

        var configFileOverridden = config["ConfigFile"];
        if (!string.IsNullOrWhiteSpace(configFileOverridden))
        {
            if (!File.Exists(configFileOverridden))
            {
                throw new FileNotFoundException("The specified ConfigFile could not be found.", configFileOverridden);
            }

            builder.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), configFileOverridden), false, false);
            Log.Warning("Overriding settings with '{ConfigFile}'", configFileOverridden);
        }

        return builder;
    }


    private static IConfigurationBuilder AddEnvironmentVariablesAndCommandLine(this IConfigurationBuilder builder, string[] args)
    {
        return builder
               .AddEnvironmentVariables("ASPNETCORE_")
               .AddEnvironmentVariables("AEGS_")
               .AddCommandLine(args);
    }
}
