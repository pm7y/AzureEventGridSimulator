using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
                var environmentName = WebHost
                                      .CreateDefaultBuilder(args)
                                      .GetSetting("ENVIRONMENT");

                Log.Logger = new LoggerConfiguration()
                             .Enrich.FromLogContext()
                             .Enrich.WithProperty("AspNetCoreEnvironment", environmentName)
                             .Enrich.WithProperty("ApplicationName", nameof(AzureEventGridSimulator))
                             .Enrich.WithMachineName()
                             .MinimumLevel.Debug()
                             .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                             .MinimumLevel.Override("System", LogEventLevel.Warning)
                             .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                             .WriteTo.Console()
                             .WriteTo.Seq("http://localhost:5341/")
                             .CreateLogger();

                var host = WebHost
                           .CreateDefaultBuilder(args)
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
                           .UseStartup<Startup>()
                           .UseKestrel(options =>
                           {
                               var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));
                               var enabledTopics = simulatorSettings.Topics.Where(t => !t.Disabled);

                               foreach (var topics in enabledTopics)
                               {
                                   options.Listen(IPAddress.Any,
                                                  topics.Port,
                                                  listenOptions => { listenOptions.UseHttps(StoreName.My, "localhost", true); });
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
