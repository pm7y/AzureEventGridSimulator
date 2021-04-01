using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger=Serilog.ILogger;

namespace AzureEventGridSimulator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Build it and fire it up
                CreateWebHostBuilder(args)
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to start the Azure Event Grid Simulator");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            // Set up basic Console logger we can use to log to until we've finished building everything
            Log.Logger = CreateBasicConsoleLogger();

            // First thing's first. Build the configuration.
            var configuration = BuildConfiguration(args);

            // Configure the web host builder
            return ConfigureWebHost(args, configuration);
        }

        private static ILogger CreateBasicConsoleLogger()
        {
            return new LoggerConfiguration()
                   .MinimumLevel.Is(LogEventLevel.Verbose)
                   .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                   .MinimumLevel.Override("System", LogEventLevel.Error)
                   .WriteTo.Console()
                   .CreateBootstrapLogger();
        }

        private static IConfigurationRoot BuildConfiguration(string[] args)
        {
            var environmentAndCommandLineConfiguration = new ConfigurationBuilder()
                                                         .AddEnvironmentVariablesAndCommandLine(args)
                                                         .Build();

            var environmentName = environmentAndCommandLineConfiguration.EnvironmentName();

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", true, false)
                          .AddJsonFile($"appsettings.{environmentName}.json", true, false)
                          .AddCustomSimulatorConfigFileIfSpecified(environmentAndCommandLineConfiguration)
                          .AddEnvironmentVariablesAndCommandLine(args)
                          .AddInMemoryCollection(
                                                 new Dictionary<string, string>
                                                 {
                                                     ["AEGS_Serilog__Using__0"] = "Serilog.Sinks.Console",
                                                     ["AEGS_Serilog__Using__1"] = "Serilog.Sinks.File",
                                                     ["AEGS_Serilog__Using__2"] = "Serilog.Sinks.Seq"
                                                 });

            return builder.Build();
        }

        private static IWebHostBuilder ConfigureWebHost(string[] args, IConfiguration configuration)
        {
            return WebHost
                   .CreateDefaultBuilder<Startup>(args)
                   .ConfigureAppConfiguration((_, builder) =>
                   {
                       builder.Sources.Clear();
                       builder.AddConfiguration(configuration);
                   })
                   .ConfigureLogging(builder => { builder.ClearProviders(); })
                   .UseSerilog((context, loggerConfiguration) =>
                   {
                       var hasAtLeastOneLogSinkBeenConfigured = context.Configuration.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

                       loggerConfiguration
                           .Enrich.FromLogContext()
                           .Enrich.WithProperty("MachineName", Environment.MachineName)
                           .Enrich.WithProperty("Environment", context.Configuration.EnvironmentName())
                           .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                           .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version)
                           // The sensible defaults
                           .MinimumLevel.Is(LogEventLevel.Information)
                           .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                           .MinimumLevel.Override("System", LogEventLevel.Error)
                           // Override defaults from settings if any
                           .ReadFrom.Configuration(context.Configuration, "Serilog")
                           .WriteTo.Conditional(_ => !hasAtLeastOneLogSinkBeenConfigured, sinkConfiguration => sinkConfiguration.Console());
                   })
                   .UseKestrel(options =>
                   {
                       Log.Verbose(((IConfigurationRoot)configuration).GetDebugView().Normalize());

                       options.ConfigureSimulatorCertificate();

                       foreach (var topics in options.ApplicationServices.EnabledTopics())
                       {
                           options.Listen(IPAddress.Any,
                                          topics.Port,
                                          listenOptions => listenOptions
                                              .UseHttps());
                       }
                   });
        }
    }
}
