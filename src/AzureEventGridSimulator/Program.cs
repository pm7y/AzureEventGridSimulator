using System.Net;
using System.Security.Cryptography.X509Certificates;
using AzureEventGridSimulator.Settings;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args)
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
                       .Build();

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          .UseKestrel(options =>
                          {
                              var settings = SettingsHelper.GetSimulatorSettings();

                              foreach (var topics in settings.Topics)
                              {
                                  options.Listen(IPAddress.Loopback, topics.Port,
                                                 listenOptions => { listenOptions.UseHttps(StoreName.My, "localhost", true); });
                              }
                          })
                          .UseStartup<Startup>();
        }
    }
}
