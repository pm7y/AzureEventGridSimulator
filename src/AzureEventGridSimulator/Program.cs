using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AzureEventGridSimulator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var host = WebHost
                           .CreateDefaultBuilder<Startup>(args)
                           .ConfigureAppConfiguration((context, builder) =>
                           {
                               var configRoot = builder.Build();

                               // System.IO.File.WriteAllText("appsettings.debug.txt", configRoot.GetDebugView());

                               var atLeastOneSinkExists = configRoot.GetSection("Serilog:WriteTo").GetChildren().ToArray().Any();

                               var logConfig = new LoggerConfiguration()
                                               .Enrich.FromLogContext()
                                               .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                                               .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                                               .Enrich.WithMachineName()
                                               .MinimumLevel.Is(LogEventLevel.Information)
                                               .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                                               .MinimumLevel.Override("System", LogEventLevel.Error)
                                               .ReadFrom.Configuration(configRoot, "Serilog");

                               if (!atLeastOneSinkExists)
                               {
                                   logConfig = logConfig.WriteTo.Console();
                               }

                               Log.Logger = logConfig.CreateLogger();

                               Log.Logger.Information("It's alive!");
                           })
                           .ConfigureLogging((context, builder) => { builder.ClearProviders(); })
                           .UseSerilog()
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
                           })
                           .UseUrls("https://127.0.0.1:0") // The default which we'll override with the configured topics
                           .UseKestrel(options =>
                           {
                               var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));
                               var enabledTopics = simulatorSettings.Topics.Where(t => !t.Disabled);

                               var cert = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path");
                               var certPass = Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password");

                               X509Certificate2 certificate = null;
                               if (string.IsNullOrWhiteSpace(cert) == false && string.IsNullOrWhiteSpace(certPass) == false) {
                                   Log.Logger.Warning("ASPNETCORE_Kestrel__Certificates__Default__Path is define, using '{ASPNETCORE_Kestrel__Certificates__Default__Path}'", cert);
                                   certificate = new X509Certificate2(cert, certPass);
                               }

                               foreach (var topics in enabledTopics)
                               {
                                   options.Listen(IPAddress.Any,
                                                  topics.Port,
                                                  listenOptions =>
                                                  {
                                                      if (certificate != null) {
                                                        listenOptions
                                                            .UseHttps(httpsOptions => httpsOptions.ServerCertificateSelector = (features, name) => certificate)
                                                            .UseConnectionLogging();
                                                      }
                                                      else
                                                      {
                                                          listenOptions
                                                              .UseHttps(StoreName.My, "localhost", true)
                                                              .UseConnectionLogging();
                                                      }
                                                  });
                               }
                           })
                           .Build();

                try
                {
                    host.Run();
                }
                catch (Exception ex)
                {
                    Log.Logger.Fatal($"Error running the Azure Event Grid Simulator: {ex.Message}");
                }
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
