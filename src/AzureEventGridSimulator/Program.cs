using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Serilog;
using Serilog.Events;

namespace AzureEventGridSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Verbose()
                         .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                         .MinimumLevel.Override("System", LogEventLevel.Warning)
                         .Enrich.FromLogContext()
                         .WriteTo.Console()
                         .CreateLogger();

            try
            {
                Log.Information("Starting Azure Event Grid Simulator...");

                var host = CreateWebHostBuilder(args)
                           .UseSerilog()
                           .Build();

                host.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Terminated unexpectedly.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          .UseKestrel(options =>
                          {
                              var settings = SettingsHelper.GetSimulatorSettings();

                              foreach (var topics in settings.Topics)
                              {
                                  options.Listen(IPAddress.Loopback, topics.HttpsPort,
                                                 listenOptions => { listenOptions.UseHttps(StoreName.My, "localhost", true); });
                              }
                          })
                          .UseStartup<Startup>();
        }
    }
}
