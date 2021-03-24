using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
        private static IWebHostBuilder ConfigureWebHost(string[] args, IConfiguration configuration)
        {
            return WebHost
                   .CreateDefaultBuilder<Startup>(args)
                   .UseConfiguration(configuration)
                   .ConfigureLogging(builder => { builder.ClearProviders(); })
                   .UseSerilog()
                   .UseKestrel(options =>
                   {
                       var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));

                       if (simulatorSettings?.Topics is null)
                       {
                           throw new InvalidOperationException("No settings found!");
                       }

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
                                                  .UseHttps(httpsOptions => httpsOptions.ServerCertificateSelector = (_, _) => certificate));
                           }
                           else
                           {
                               // Use the dev cert on localhost. We have to run on https (It's all Microsoft.Azure.EventGrid) supports).
                               options.ListenLocalhost(topics.Port, listenOptions => listenOptions.UseHttps());
                           }
                       }
                   });
        }

        public static void Main(string[] args)
        {
            try
            {
                var webHost = CreateWebHostBuilder(args).Build();

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

        private static IWebHostBuilder CreateWebHostBuilder(string[] args)
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

            var hostBuilder = ConfigureWebHost(args, config);

            return hostBuilder;
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

        private static void CreateLogger(IConfiguration config, string environmentName)
        {
            var atLeastOneLogHasBeenConfigured = config.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

            var logConfig = new LoggerConfiguration()
                            .Enrich.FromLogContext()
                            .Enrich.WithProperty("MachineName", Environment.MachineName)
                            .Enrich.WithProperty("Environment", environmentName)
                            .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                            .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version)
                            // The sensible defaults
                            .MinimumLevel.Is(LogEventLevel.Information)
                            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                            .MinimumLevel.Override("System", LogEventLevel.Error)
                            // Override defaults from settings if any
                            .ReadFrom.Configuration(config, "Serilog");

            if (!atLeastOneLogHasBeenConfigured)
            {
                logConfig = logConfig.WriteTo.Console();
            }

            // Serilog.Debugging.SelfLog.Enable(s => Console.WriteLine($"Serilog Debug -> {s}"));

            Log.Logger = logConfig.CreateLogger();

            ShowSerilogUsingWarningIfNecessary(config);
        }

        private static void ShowSerilogUsingWarningIfNecessary(IConfiguration config)
        {
            var usingNeedsToBeConfigured = config.GetSection("Serilog").Exists() &&
                                           !config.GetSection("Serilog:Using").Exists();

            if (usingNeedsToBeConfigured)
            {
                // Warn the user about the necessity for the serilog using section with .net 5.0.
                // https://github.com/serilog/serilog-settings-configuration#net-50-single-file-applications
                Console.WriteLine(@"The Azure Event Grid simulator was unable to start: -" + Environment.NewLine);
                Console.WriteLine(@"   Serilog with .net 5.0 now requires a 'Using' section.");
                Console.WriteLine(@"   https://github.com/serilog/serilog-settings-configuration#net-50-single-file-applications" +
                                  Environment.NewLine);
                Console.WriteLine(
                                  @"Please add the following to the Serilog config section and restart: -" + Environment.NewLine);
                Console.WriteLine(@"   ""Using"": [""Serilog.Sinks.Console"", ""Serilog.Sinks.File"", ""Serilog.Sinks.Seq""]" +
                                  Environment.NewLine);

                Console.WriteLine(@"Any key to exit...");
                Console.ReadKey();
                Environment.Exit(-1);
            }
        }
    }
}
