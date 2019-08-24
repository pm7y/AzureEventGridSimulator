using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AzureEventGridSimulator
{
    public static class Program
    {
        public static void Main()
        {
            try
            {
                var host = WebHost.CreateDefaultBuilder()
                                  .ConfigureAppConfiguration((context, builder) =>
                                  {
                                      var env = context.HostingEnvironment;

                                      var config = builder.AddJsonFile("appsettings.json", false, false)
                                                          .AddJsonFile($"appsettings.{env.EnvironmentName.ToLowerInvariant().Trim()}.json", true, false)
                                                          .AddEnvironmentVariables()
                                                          .Build();

                                      Log.Logger = new LoggerConfiguration()
                                                   .Enrich.FromLogContext()
                                                   .Enrich.WithProperty("AspNetCoreEnvironment", env.EnvironmentName)
                                                   .Enrich.WithMachineName()
                                                   .MinimumLevel.Debug()
                                                   .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                                                   .MinimumLevel.Override("System", LogEventLevel.Error)
                                                   .ReadFrom.Configuration(config)
                                                   .CreateLogger();
                                  })
                                  .UseStartup<Startup>()
                                  .UseSerilog()
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
                    logger.LogError(ex, "Failed to run the Azure Event Grid Simulator.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                Log.CloseAndFlush();
            }

            Console.WriteLine("");
            Console.WriteLine("Any key to exit...");
            Console.ReadKey();
        }
    }
}
