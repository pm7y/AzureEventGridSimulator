using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AzureEventGridSimulator
{
    public static class Program
    {
        public static IWebHostBuilder CreateWebHostBuilder(string[] args, IConfiguration configuration)
        {
            return WebHost
                   .CreateDefaultBuilder<Startup>(args)
                   .UseConfiguration(configuration)
                   .ConfigureLogging(builder => builder.ClearProviders())
                   .UseSerilog()
                   .UseKestrel(options =>
                   {
                       var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));
                       var enabledTopics = simulatorSettings.Topics.Where(t => !t.Disabled);

                       var cert = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path");
                       var certPass = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password");

                       X509Certificate2 certificate = null;
                       if (string.IsNullOrWhiteSpace(cert) == false && string.IsNullOrWhiteSpace(certPass) == false)
                       {
                           Log.Warning("ASPNETCORE_Kestrel__Certificates__Default__Path is defined, using '{ASPNETCORE_Kestrel__Certificates__Default__Path}'", cert);
                           certificate = new X509Certificate2(cert, certPass);
                       }

                       options.ConfigureHttpsDefaults(httpsOptions => { httpsOptions.SslProtocols = SslProtocols.Tls12; });

                       foreach (var topics in enabledTopics)
                       {
                           if (certificate != null)
                           {
                               options.Listen(IPAddress.Any,
                                              topics.Port,
                                              listenOptions => listenOptions
                                                  .UseHttps(httpsOptions => httpsOptions.ServerCertificateSelector = (features, name) => certificate));
                           }
                           else
                           {
                               // Use the dev cert on localhost. We have to run on https (It's all Microsoft.Azure.EventGrid) supports).
                               options.ListenLocalhost(topics.Port, listenOptions => listenOptions.UseHttps());
                           }
                       }

                       Log.Information("It's alive!");
                   });
        }

        public static void Main(string[] args)
        {
            try
            {
                var webHost = CreateWebHost(args);

                webHost.Run();
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

        public static IWebHost CreateWebHost(string[] args)
        {
            var environmentName = GetHostingEnvironment();

            var builder = new ConfigurationBuilder()
                          .SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json", true, false)
                          .AddJsonFile($"appsettings.{environmentName}.json", true, false)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);

            var configFileOverriddenFromCommandLine = builder.Build().GetValue<string>("ConfigFile");
            if (!string.IsNullOrWhiteSpace(configFileOverriddenFromCommandLine))
            {
                // The path to the config file has been passed at the command line
                // e.g. AzureEventGridSimulator.exe --ConfigFile=/path/to/config.json
                builder.AddJsonFile(configFileOverriddenFromCommandLine, false, false);
                Log.Warning("Overriding settings with '{SettingsPath}'", configFileOverriddenFromCommandLine);
            }

            var config = builder.Build();

            // You can uncomment this to get a dump of the current effective config settings.
            // System.IO.File.WriteAllText("appsettings.debug.txt", config.GetDebugView());

            CreateLogger(config, environmentName);

            var host = CreateWebHostBuilder(args, config).Build();

            return host;
        }

        private static string GetHostingEnvironment()
        {
            var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (string.IsNullOrWhiteSpace(environmentName))
            {
                environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            }

            if (string.IsNullOrWhiteSpace(environmentName))
            {
                environmentName = "Production";
            }

            return environmentName;
        }

        private static void CreateLogger(IConfigurationRoot config, string environmentName)
        {
            var logConfig = new LoggerConfiguration()
                            .Enrich.FromLogContext()
                            .Enrich.WithProperty("MachineName", Environment.MachineName)
                            .Enrich.WithProperty("Environment", environmentName)
                            .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                            // The sensible defaults
                            .MinimumLevel.Is(LogEventLevel.Information)
                            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                            .MinimumLevel.Override("System", LogEventLevel.Error)
                            // Override defaults from settings if any
                            .ReadFrom.Configuration(config, "Serilog");

            var atLeastOneLogHasBeenConfigured = config.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

            if (!atLeastOneLogHasBeenConfigured)
            {
                // If no sinks have been define then add the console.
                logConfig = logConfig.WriteTo.Console();
            }

            Log.Logger = logConfig.CreateLogger();
        }
    }

    public static class WebHostBuilderExtensions
    {
        public static IWebHostBuilder UseStuff(this IWebHostBuilder hostBuilder)
        {
            return hostBuilder;
        }
    }
}
