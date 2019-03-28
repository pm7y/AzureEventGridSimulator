using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Extensions;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https.Internal;
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
                                      logging.AddConsole(options =>
                                      {
                                          options.IncludeScopes = true;
                                          options.DisableColors = false;
                                      });
                                      logging.AddDebug();

                                      logging.SetMinimumLevel(LogLevel.Debug);

                                      logging.AddFilter("System", LogLevel.Warning);
                                      logging.AddFilter("Microsoft", LogLevel.Warning);
                                  })
                                  .UseKestrel(options =>
                                  {
                                      var simulatorSettings = (SimulatorSettings)options.ApplicationServices.GetService(typeof(SimulatorSettings));
                                      var certificate = new X509Certificate2("server.pfx", "password");

                                      foreach (var topics in simulatorSettings.Topics)
                                      {
                                          options.ListenAnyIP(topics.Port, listenOptions =>
                                          {
                                              listenOptions.UseHttps(httpsOptions =>
                                              {
                                                  httpsOptions.ServerCertificateSelector = (features, name) =>
                                                  {
                                                      // Here you would check the name, select an appropriate cert, and provide a fallback or fail for null names.
                                                      return certificate;
                                                  };
                                              });
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
