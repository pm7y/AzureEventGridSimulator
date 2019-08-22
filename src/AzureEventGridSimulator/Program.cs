using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var host = WebHost.CreateDefaultBuilder()
                                  .UseSimulatorSettings()
                                  .UseStartup<Startup>()
                                  .ConfigureLogging((hostingContext, logging) =>
                                  {
                                      logging.ClearProviders();

                                      logging.AddConsole(options =>
                                      {
                                          options.IncludeScopes = true;
                                          options.DisableColors = false;
                                      });

                                      logging.SetMinimumLevel(LogLevel.Debug);

                                      logging.AddFilter("System", LogLevel.Error);
                                      logging.AddFilter("Microsoft", LogLevel.Error);
                                  })
                                  .UseKestrel(options =>
                                  {
                                      var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));

                                      foreach (var topics in simulatorSettings.Topics)
                                      {
                                          options.Listen(IPAddress.Any, topics.Port,
                                                         listenOptions =>
                                                         {
                                                             listenOptions.UseHttps(StoreName.My, "localhost", true);
                                                         });
                                      }
                                  })
                                  .Build();

                var logger = (ILogger)host.Services.GetService(typeof(ILogger));
                logger.LogInformation("Started");

                try
                {
                    host.Run();
                }
                catch (Exception ex)
                {
                    logger.LogError("Failed to run the Azure Event Grid Simulator: {ErrorMessage}.", ex.Message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("");
            Console.WriteLine("Any key to exit...");
            Console.ReadKey();
        }
    }
}
