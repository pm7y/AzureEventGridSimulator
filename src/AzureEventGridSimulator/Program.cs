using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Asp.Versioning;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Commands;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Dashboard;
using AzureEventGridSimulator.Domain.Services.Delivery;
using AzureEventGridSimulator.Domain.Services.Retry;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Dashboard;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Infrastructure.Mediator;
using AzureEventGridSimulator.Infrastructure.Middleware;
using AzureEventGridSimulator.Infrastructure.Settings;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;

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

            // Conditionally enable dashboard based on settings
            var simulatorSettings = app.Services.GetRequiredService<SimulatorSettings>();
            if (simulatorSettings.DashboardEnabled)
            {
                app.UseDashboard();
            }

            app.UseRouting();
            app.MapControllers();

#if ASPIRE_ENABLED
            // Map Aspire health check endpoints (/health, /alive)
            app.MapDefaultEndpoints();
#endif

            if (simulatorSettings.DashboardEnabled)
            {
                app.MapDashboardEndpoints();
            }

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
            await host.StartAsync(token).ConfigureAwait(false);
            await OnApplicationStarted(host, host.Lifetime).ConfigureAwait(false);
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

            var simulatorSettings = app.ApplicationServices.GetRequiredService<SimulatorSettings>();

            if (simulatorSettings.Topics.Length == 0)
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

            var mediator = app.ApplicationServices.GetRequiredService<IMediator>();

            await mediator.Send(new ValidateAllSubscriptionsCommand());

            // Log all configured subscribers
            foreach (var topic in simulatorSettings.Topics.Where(t => !t.Disabled))
            {
                var allSubscribers = topic.Subscribers.All.ToList();
                Log.Information(
                    "Topic '{TopicName}' (port {Port}) has {Count} subscriber(s)",
                    topic.Name,
                    topic.Port,
                    allSubscribers.Count
                );

                foreach (var sub in allSubscribers)
                {
                    Log.Information(
                        "  - {SubscriberName} ({SubscriberType}){Disabled}",
                        sub.Name,
                        sub.SubscriberType,
                        sub.Disabled ? " [DISABLED]" : ""
                    );
                }
            }

            // Log dashboard availability
            // Note: Validation ensures DashboardPort is set or at least one topic is enabled
            if (simulatorSettings.DashboardEnabled)
            {
                var dashboardPort =
                    simulatorSettings.DashboardPort
                    ?? simulatorSettings.Topics.First(t => !t.Disabled).Port;

                Log.Information(
                    "Dashboard available at https://localhost:{Port}/dashboard",
                    dashboardPort
                );
            }

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
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

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
            "For more information, visit: https://github.com/pm7y/AzureEventGridSimulator"
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

    private static ReloadableLogger CreateBasicConsoleLogger()
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
                new Dictionary<string, string?>
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

#if ASPIRE_ENABLED
        // Add Aspire service defaults (OpenTelemetry, health checks, service discovery)
        builder.AddServiceDefaults();
#endif

        builder.Services.AddSimulatorSettings(configuration);
        builder.Services.AddMediator(Assembly.GetExecutingAssembly());

        // Register TimeProvider for testability
        builder.Services.AddSingleton(TimeProvider.System);

        // Register event schema services (stateless, can be singletons)
        builder.Services.AddSingleton<EventSchemaDetector>();
        builder.Services.AddSingleton<EventGridSchemaParser>();
        builder.Services.AddSingleton<CloudEventSchemaParser>();
        builder.Services.AddSingleton<EventSchemaParserFactory>();
        builder.Services.AddSingleton<EventGridSchemaFormatter>();
        builder.Services.AddSingleton<CloudEventSchemaFormatter>();
        builder.Services.AddSingleton<EventSchemaFormatterFactory>();

        // Register validation services (routing and validation pipeline)
        builder.Services.AddEventGridValidation();

        // Register delivery services
        builder.Services.AddSingleton<DeliveryPropertyResolver>();
        builder.Services.AddSingleton<ServiceBusEventDeliveryService>();
        builder.Services.AddSingleton<StorageQueueEventDeliveryService>();
        builder.Services.AddSingleton<EventHubEventDeliveryService>();
        builder.Services.AddSingleton<HttpEventDeliveryService>();

        // Register retry and dead-letter services
        builder.Services.AddSingleton<RetryScheduler>();
        builder.Services.AddSingleton<IDeliveryQueue, InMemoryDeliveryQueue>();
        builder.Services.AddSingleton<DeadLetterService>();
        builder.Services.AddHostedService<RetryDeliveryBackgroundService>();

        // Register dashboard services
        builder.Services.AddSingleton<EventHistoryStore>();
        builder.Services.AddSingleton<IEventHistoryService, EventHistoryService>();

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
                    DateOnly.Parse(Constants.SupportedApiVersion, CultureInfo.InvariantCulture)
                );
                options.AssumeDefaultVersionWhenUnspecified = true;
                // Don't auto-add api-supported-versions header - we add it manually where needed to match Azure behavior
                options.ReportApiVersions = false;
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
                        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0"
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
            var debugView = ((IConfigurationRoot)configuration).GetDebugView().Normalize();
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            Log.Verbose(debugView);

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
