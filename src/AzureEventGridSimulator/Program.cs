using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

[assembly: InternalsVisibleTo("AzureEventGridSimulator.Tests")]

namespace AzureEventGridSimulator;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            // Build it and fire it up
            var app = CreateWebHostBuilder(args).Build();

            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (_, _, _) => LogEventLevel.Debug;
            });
            app.UseEventGridMiddleware();
            app.UseRouting();
            app.MapControllers();

            await StartSimulator(app);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to start the Azure Event Grid Simulator");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    public static async Task StartSimulator(WebApplication host, CancellationToken token = default)
    {
        try
        {
            await host.StartAsync(token)
                .ContinueWith(_ => OnApplicationStarted(host, host.Lifetime), token)
                .ConfigureAwait(false);

            await host.WaitForShutdownAsync(token).ConfigureAwait(false);
        }
        finally
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task OnApplicationStarted(
        IApplicationBuilder app,
        IHostApplicationLifetime lifetime
    )
    {
        try
        {
            Log.Verbose("Started");

            var simulatorSettings = app.ApplicationServices.GetService<SimulatorSettings>();

            if (simulatorSettings is null || simulatorSettings.Topics.Length == 0)
            {
                DisplayConfigurationHelp();
                lifetime.StopApplication();
                return;
            }

            if (simulatorSettings.Topics.All(o => o.Disabled))
            {
                Log.Fatal(
                    "All of the configured topics are disabled. The application will now exit"
                );
                lifetime.StopApplication();
                return;
            }

            var mediator = app.ApplicationServices.GetService<IMediator>();

            if (mediator is null)
            {
                Log.Fatal("Required component was not found. The application will now exit");
                lifetime.StopApplication();
                return;
            }

            await mediator.Send(new ValidateAllSubscriptionsCommand());

            Log.Information("It's alive !");
        }
        catch (Exception e)
        {
            Log.Fatal(e, "It died !");
            lifetime.StopApplication();
        }
    }

    private static void DisplayConfigurationHelp()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";

        Console.WriteLine();
        Console.WriteLine($"Azure Event Grid Simulator v{version}");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine();
        Console.WriteLine(
            "No topics configured. To get started, provide configuration using one of these methods:"
        );
        Console.WriteLine();
        Console.WriteLine("1. Create an appsettings.json file in the current directory:");
        Console.WriteLine();
        Console.WriteLine("   {");
        Console.WriteLine("     \"topics\": [");
        Console.WriteLine("       {");
        Console.WriteLine("         \"name\": \"MyTopic\",");
        Console.WriteLine("         \"port\": 60101,");
        Console.WriteLine("         \"key\": \"MyAccessKey=\",");
        Console.WriteLine("         \"subscribers\": [");
        Console.WriteLine("           {");
        Console.WriteLine("             \"name\": \"MySubscriber\",");
        Console.WriteLine("             \"endpoint\": \"https://localhost:5000/api/events\",");
        Console.WriteLine("             \"disableValidation\": true");
        Console.WriteLine("           }");
        Console.WriteLine("         ]");
        Console.WriteLine("       }");
        Console.WriteLine("     ]");
        Console.WriteLine("   }");
        Console.WriteLine();
        Console.WriteLine("2. Use the --ConfigFile argument to specify a config file path:");
        Console.WriteLine();
        Console.WriteLine("   azure-eventgrid-simulator --ConfigFile=/path/to/config.json");
        Console.WriteLine();
        Console.WriteLine("3. Use environment variables with the AEGS_ prefix:");
        Console.WriteLine();
        Console.WriteLine(
            "   AEGS_topics__0__name=MyTopic AEGS_topics__0__port=60101 azure-eventgrid-simulator"
        );
        Console.WriteLine();
        Console.WriteLine(
            "For more information, visit: https://github.com/pmcilreavy/AzureEventGridSimulator"
        );
        Console.WriteLine();
    }

    private static WebApplicationBuilder CreateWebHostBuilder(string[] args)
    {
        // Set up basic Console logger we can use to log to until we've finished building everything
        Log.Logger = CreateBasicConsoleLogger();

        // First thing's first. Build the configuration.
        var configuration = BuildConfiguration(args);

        // Configure the web host builder
        return ConfigureWebHost(args, configuration);
    }

    private static ILogger CreateBasicConsoleLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Is(LogEventLevel.Verbose)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
            .MinimumLevel.Override("System", LogEventLevel.Error)
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    private static IConfigurationRoot BuildConfiguration(string[] args)
    {
        var environmentAndCommandLineConfiguration = new ConfigurationBuilder()
            .AddEnvironmentVariablesAndCommandLine(args)
            .Build();

        var environmentName = environmentAndCommandLineConfiguration.EnvironmentName();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, false)
            .AddJsonFile($"appsettings.{environmentName}.json", true, false)
            .AddCustomSimulatorConfigFileIfSpecified(environmentAndCommandLineConfiguration)
            .AddEnvironmentVariablesAndCommandLine(args)
            .AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    ["AEGS_Serilog__Using__0"] = "Serilog.Sinks.Console",
                    ["AEGS_Serilog__Using__1"] = "Serilog.Sinks.File",
                    ["AEGS_Serilog__Using__2"] = "Serilog.Sinks.Seq",
                }
            );

        return builder.Build();
    }

    private static WebApplicationBuilder ConfigureWebHost(
        string[] args,
        IConfiguration configuration
    )
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSimulatorSettings(configuration);
        builder.Services.AddMediator(Assembly.GetExecutingAssembly());

        // Register event schema services (stateless, can be singletons)
        builder.Services.AddSingleton<EventSchemaDetector>();
        builder.Services.AddSingleton<EventGridSchemaParser>();
        builder.Services.AddSingleton<CloudEventSchemaParser>();
        builder.Services.AddSingleton<EventSchemaParserFactory>();
        builder.Services.AddSingleton<EventGridSchemaFormatter>();
        builder.Services.AddSingleton<CloudEventSchemaFormatter>();
        builder.Services.AddSingleton<EventSchemaFormatterFactory>();

        // Register delivery services
        builder.Services.AddSingleton<DeliveryPropertyResolver>();
        builder.Services.AddSingleton<ServiceBusEventDeliveryService>();
        builder.Services.AddSingleton<StorageQueueEventDeliveryService>();

        var httpClientBuilder = builder.Services.AddHttpClient(nameof(AzureEventGridSimulator));
        if (configuration.GetValue<bool>("dangerousAcceptAnyServerCertificateValidator"))
        {
            Log.Warning(
                "DangerousAcceptAnyServerCertificateValidator is enabled. This should only be used for testing purposes"
            );
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                }
            );
        }

        builder.Services.AddScoped<SasKeyValidator>();
        builder.Services.AddSingleton<ValidationIpAddressProvider>();

        builder
            .Services.AddControllers(options =>
            {
                options.EnableEndpointRouting = false;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = true;
            });

        builder
            .Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(
                    DateOnly.Parse(Constants.SupportedApiVersion)
                );
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc();

        builder.Logging.ClearProviders();
        builder.Host.UseSerilog(
            (context, loggerConfiguration) =>
            {
                var hasAtLeastOneLogSinkBeenConfigured =
                    context
                        .Configuration.GetSection("Serilog:WriteTo")
                        .GetChildren()
                        .ToArray()
                        .Length != 0;

                loggerConfiguration
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("MachineName", Environment.MachineName)
                    .Enrich.WithProperty("Environment", context.Configuration.EnvironmentName())
                    .Enrich.WithProperty("Application", nameof(AzureEventGridSimulator))
                    .Enrich.WithProperty(
                        "Version",
                        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown"
                    )
                    // The sensible defaults
                    .MinimumLevel.Is(LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Error)
                    .MinimumLevel.Override("System", LogEventLevel.Error)
                    // Override defaults from settings if any
                    .ReadFrom.Configuration(context.Configuration)
                    .WriteTo.Conditional(
                        _ => !hasAtLeastOneLogSinkBeenConfigured,
                        sinkConfiguration => sinkConfiguration.Console()
                    );
            }
        );

        builder.Configuration.AddConfiguration(configuration);
        builder.WebHost.UseKestrel(options =>
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.Verbose(((IConfigurationRoot)configuration).GetDebugView().Normalize());

            options.ConfigureSimulatorCertificate();

            foreach (var topics in options.ApplicationServices.EnabledTopics())
            {
                options.Listen(
                    IPAddress.Any,
                    topics.Port,
                    listenOptions => listenOptions.UseHttps()
                );
            }
        });

        return builder;
    }
}
