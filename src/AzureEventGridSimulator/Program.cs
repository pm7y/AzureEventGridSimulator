using System;
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
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost
                .CreateDefaultBuilder<Startup>(args)
                .ConfigureAppConfiguration((context, builder) =>
                {
                    var configFileOverriddenFromCommandLine = context.Configuration.GetValue<string>("ConfigFile");
                    if (!string.IsNullOrWhiteSpace(configFileOverriddenFromCommandLine))
                    {
                        // The path to the config file has been passed at the command line
                        // e.g. AzureEventGridSimulator.exe --ConfigFile=/path/to/config.json
                        builder.AddJsonFile(configFileOverriddenFromCommandLine, optional: false);
                        Log.Logger.Warning("Overriding settings with '{SettingsPath}'", configFileOverriddenFromCommandLine);
                    }

                    // You can uncomment this to get a dump of the current effective config settings.
                    //System.IO.File.WriteAllText("appsettings.debug.txt", builder.Build().GetDebugView());
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.ClearProviders();

                    var atLeastOneLogHasBeenConfigured = context.Configuration.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

                    var logConfig = new LoggerConfiguration()
                                    .Enrich.FromLogContext()
                                    .Enrich.WithMachineName()
                                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                                    .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                                    .MinimumLevel.Is(LogEventLevel.Information)
                                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                                    .MinimumLevel.Override("System", LogEventLevel.Error)
                                    .ReadFrom.Configuration(context.Configuration, "Serilog");

                    if (!atLeastOneLogHasBeenConfigured)
                    {
                        logConfig = logConfig.WriteTo.Console();
                    }

                    Log.Logger = logConfig.CreateLogger();
                    Log.Logger.Information("It's alive!");
                })
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
                        Log.Logger.Warning("ASPNETCORE_Kestrel__Certificates__Default__Path is defined, using '{ASPNETCORE_Kestrel__Certificates__Default__Path}'", cert);
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
                });

        public static void Main(string[] args)
        {
            try
            {
                var host = CreateWebHostBuilder(args)
                    .Build();
                host.Run();
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal($"Failed to start the Azure Event Grid Simulator: {ex.Message}");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
