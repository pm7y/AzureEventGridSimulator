var builder = DistributedApplication.CreateBuilder(args);

// Path to simulator configuration file
var configFile = Path.Combine(
    Projects.AzureEventGridSimulator_AppHost.ProjectPath,
    "simulator-config.json"
);

// Azure emulators for local development
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var queues = storage.AddQueues("queues");
var blobs = storage.AddBlobs("blobs");

var serviceBus = builder.AddAzureServiceBus("servicebus").RunAsEmulator();
var serviceBusQueue = serviceBus.AddServiceBusQueue("sb-events");

var eventHubs = builder.AddAzureEventHubs("eventhubs").RunAsEmulator();
var eventHub = eventHubs.AddHub("eh-events");

var sql = builder.AddSqlServer("sql");

// Add the Event Grid Simulator
// Use isProxied: false because the simulator listens on multiple ports (one per topic)
// and Aspire's default reverse proxy doesn't support this scenario
builder
    .AddProject<Projects.AzureEventGridSimulator>("simulator", launchProfileName: null)
    .WithHttpsEndpoint(port: 60101, name: "default", isProxied: false)
    .WithReference(queues)
    .WithReference(blobs)
    .WithReference(serviceBusQueue)
    .WithReference(eventHub)
    .WithReference(sql)
    .WaitFor(storage)
    .WaitFor(serviceBus)
    .WaitFor(eventHubs)
    .WaitFor(sql)
    // Load topic/subscriber configuration from file
    .WithArgs($"--ConfigFile={configFile}")
    // Inject connection strings for Azure emulators (AEGS_ prefix required by simulator)
    .WithEnvironment(ctx =>
    {
        ctx.EnvironmentVariables["AEGS_topics__0__subscribers__storageQueue__0__connectionString"] =
            queues.Resource.ConnectionStringExpression;
        ctx.EnvironmentVariables["AEGS_topics__0__subscribers__serviceBus__0__connectionString"] =
            serviceBus.Resource.ConnectionStringExpression;
        ctx.EnvironmentVariables["AEGS_topics__0__subscribers__eventHub__0__connectionString"] =
            eventHubs.Resource.ConnectionStringExpression;
    });

builder.Build().Run();
