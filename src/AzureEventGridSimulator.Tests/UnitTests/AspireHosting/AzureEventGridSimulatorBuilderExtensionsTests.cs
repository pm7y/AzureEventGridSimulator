#pragma warning disable ASPIRECERTIFICATES001 // Asserting on Aspire's experimental certificate types

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.AspireHosting;

[Trait("Category", "unit")]
public class AzureEventGridSimulatorBuilderExtensionsTests
{
    private static IDistributedApplicationBuilder CreateBuilder() =>
        DistributedApplication.CreateBuilder([]);

    private static async Task<Dictionary<string, object>> GetEnvironment(IResource resource)
    {
        var environment = new Dictionary<string, object>(StringComparer.Ordinal);
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
            environment
        );

        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }

        return environment;
    }

    private static string Expression(object value) =>
        value.ShouldBeOfType<ReferenceExpression>().ValueExpression;

    [Fact]
    public void GivenAName_WhenSimulatorAdded_ThenRunsThePublishedImage()
    {
        var builder = CreateBuilder();

        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.Resource.ShouldBeAssignableTo<ContainerResource>();
        var image = simulator.Resource.Annotations.OfType<ContainerImageAnnotation>().Single();
        image.Registry.ShouldBe("docker.io");
        image.Image.ShouldBe("pmcilreavy/azureeventgridsimulator");
        image.Tag.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null, "latest")]
    [InlineData("1.0.0", "latest")]
    [InlineData("1.0.0+abc123", "latest")]
    [InlineData("6.1.0", "6.1.0")]
    [InlineData("6.1.0+abc123", "6.1.0")]
    [InlineData("6.1.0-beta.1+abc123", "6.1.0-beta.1")]
    public void GivenAnAssemblyVersion_WhenImageTagParsed_ThenOnlyReleasedVersionsArePinned(
        string? informationalVersion,
        string expected
    )
    {
        AzureEventGridSimulatorBuilderExtensions
            .ParseImageTag(informationalVersion)
            .ShouldBe(expected);
    }

    [Fact]
    public void GivenASimulator_WhenAdded_ThenItServesTheDeveloperCertificateWithAPassword()
    {
        var builder = CreateBuilder();

        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        // The simulator refuses a certificate file that has no password
        var certificate = simulator
            .Resource.Annotations.OfType<HttpsCertificateAnnotation>()
            .Single();
        certificate.UseDeveloperCertificate.ShouldBe(true);
        certificate.Password.ShouldNotBeNull();
        builder.Resources.ShouldNotContain(certificate.Password);
    }

    [Fact]
    public async Task GivenACertificate_WhenConfigured_ThenKestrelIsPointedAtThePfxAndItsPassword()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");
        var pfxPath = ReferenceExpression.Create($"/certs/dev.pfx");
        var password = ReferenceExpression.Create($"secret");
        var context = new HttpsCertificateConfigurationCallbackAnnotationContext
        {
            ExecutionContext = builder.ExecutionContext,
            Resource = simulator.Resource,
            Arguments = [],
            EnvironmentVariables = [],
            CertificatePath = ReferenceExpression.Create($"/certs/dev.crt"),
            KeyPath = ReferenceExpression.Create($"/certs/dev.key"),
            CertificateWithKeyPath = ReferenceExpression.Create($"/certs/dev.pem"),
            PfxPath = pfxPath,
            Password = password,
            CancellationToken = CancellationToken.None,
        };

        foreach (
            var callback in simulator.Resource.Annotations.OfType<HttpsCertificateConfigurationCallbackAnnotation>()
        )
        {
            await callback.Callback(context);
        }

        context
            .EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Path"]
            .ShouldBe(pfxPath);
        context
            .EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Password"]
            .ShouldBe(password);
    }

    [Fact]
    public void GivenATopic_WhenAdded_ThenItHasAnHttpsEndpointOnItsPortInsideAndOutsideTheContainer()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.AddTopic("orders", 60101);

        var endpoint = simulator.Resource.Annotations.OfType<EndpointAnnotation>().Single();
        endpoint.Name.ShouldBe("orders");
        endpoint.UriScheme.ShouldBe("https");
        endpoint.Port.ShouldBe(60101);
        endpoint.TargetPort.ShouldBe(60101);
    }

    [Fact]
    public void GivenATopic_WhenAdded_ThenItHasAStateForWaitForToWatch()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        var topic = simulator.AddTopic("orders", 60101);

        // A resource with no state never counts as running, so WaitFor(topic) would hang
        var snapshot = topic.Resource.Annotations.OfType<ResourceSnapshotAnnotation>().Single();
        snapshot.InitialSnapshot.State!.Text.ShouldBe(KnownResourceStates.NotStarted);
    }

    [Fact]
    public void GivenTwoTopics_WhenAdded_ThenOnlyOneHealthCheckIsRegistered()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.AddTopic("orders", 60101);
        simulator.AddTopic("invoices", 60102);

        simulator.Resource.Annotations.OfType<HealthCheckAnnotation>().Count().ShouldBe(1);
    }

    [Fact]
    public void GivenAPortInUse_WhenTopicAdded_ThenThrows()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");
        simulator.AddTopic("orders", 60101);

        Should.Throw<DistributedApplicationException>(() => simulator.AddTopic("other", 60101));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void GivenAnInvalidPort_WhenTopicAdded_ThenThrows(int port)
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        Should.Throw<ArgumentOutOfRangeException>(() => simulator.AddTopic("orders", port));
    }

    [Fact]
    public void GivenNoTopics_WhenValidated_ThenThrows()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        Should.Throw<DistributedApplicationException>(() =>
            AzureEventGridSimulatorBuilderExtensions.EnsureHasTopics(simulator.Resource)
        );
    }

    [Fact]
    public async Task GivenTopics_WhenConfigurationWritten_ThenEachIsIndexedWithItsPortAndKey()
    {
        var builder = CreateBuilder();
        var key = builder.AddParameter("orders-key", "secret");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");
        simulator.AddTopic("orders", 60101, key);
        simulator.AddTopic("invoices", 60102);

        var environment = await GetEnvironment(simulator.Resource);

        environment["AEGS_topics__0__name"].ShouldBe("orders");
        environment["AEGS_topics__0__port"].ShouldBe("60101");
        environment["AEGS_topics__0__key"].ShouldBe(key.Resource);
        environment["AEGS_topics__1__name"].ShouldBe("invoices");
        environment["AEGS_topics__1__port"].ShouldBe("60102");
        environment.ShouldNotContainKey("AEGS_topics__1__key");
    }

    [Fact]
    public async Task GivenAWebhook_WhenConfigurationWritten_ThenTargetsTheResourceEndpointAndPath()
    {
        var builder = CreateBuilder();
        var api = builder
            .AddExecutable("api", "api", ".")
            .WithHttpEndpoint(name: "http")
            .WithHttpsEndpoint(name: "https");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.AddTopic("orders", 60101).WithWebhookSubscriber("hook", api, "api/events");

        var environment = await GetEnvironment(simulator.Resource);

        const string prefix = "AEGS_topics__0__subscribers__http__0__";
        environment[$"{prefix}name"].ShouldBe("hook");
        environment[$"{prefix}disableValidation"].ShouldBe("true");
        Expression(environment[$"{prefix}endpoint"])
            .ShouldBe("{api.bindings.https.url}/api/events");
    }

    [Fact]
    public async Task GivenAnHttpOnlyTarget_WhenWebhookAdded_ThenFallsBackToHttp()
    {
        var builder = CreateBuilder();
        var api = builder.AddExecutable("api", "api", ".").WithHttpEndpoint(name: "http");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.AddTopic("orders", 60101).WithWebhookSubscriber("hook", api);

        var environment = await GetEnvironment(simulator.Resource);

        Expression(environment["AEGS_topics__0__subscribers__http__0__endpoint"])
            .ShouldBe("{api.bindings.http.url}/");
    }

    [Fact]
    public async Task GivenEachBrokerSubscriber_WhenConfigurationWritten_ThenUsesTheSimulatorsSettingNames()
    {
        var builder = CreateBuilder();
        var broker = builder.AddConnectionString("broker");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator
            .AddTopic("orders", 60101)
            .WithStorageQueueSubscriber("sq", broker, "q1")
            .WithServiceBusQueueSubscriber("sbq", broker, "q2")
            .WithServiceBusTopicSubscriber("sbt", broker, "t1")
            .WithEventHubSubscriber("eh", broker, "h1");

        var environment = await GetEnvironment(simulator.Resource);

        const string prefix = "AEGS_topics__0__subscribers__";
        environment[$"{prefix}storageQueue__0__name"].ShouldBe("sq");
        environment[$"{prefix}storageQueue__0__queueName"].ShouldBe("q1");
        Expression(environment[$"{prefix}storageQueue__0__connectionString"])
            .ShouldBe(broker.Resource.ConnectionStringExpression.ValueExpression);
        environment[$"{prefix}serviceBus__0__queue"].ShouldBe("q2");
        environment[$"{prefix}serviceBus__1__topic"].ShouldBe("t1");
        environment[$"{prefix}eventHub__0__eventHubName"].ShouldBe("h1");
    }

    [Fact]
    public void GivenABrokerSubscriber_WhenAdded_ThenSimulatorWaitsForTheBroker()
    {
        var builder = CreateBuilder();
        var broker = builder.AddConnectionString("broker");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        simulator.AddTopic("orders", 60101).WithEventHubSubscriber("eh", broker, "h1");

        simulator
            .Resource.Annotations.OfType<WaitAnnotation>()
            .ShouldContain(w => w.Resource == broker.Resource);
    }

    [Fact]
    public void GivenAKey_WhenConnectionStringRead_ThenIncludesEndpointAndKey()
    {
        var builder = CreateBuilder();
        var key = builder.AddParameter("orders-key", "secret");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        var topic = simulator.AddTopic("orders", 60101, key);

        topic.Resource.ConnectionStringExpression.ValueExpression.ShouldBe(
            "Endpoint={eventgrid.bindings.orders.url}/api/events;Key={orders-key.value}"
        );
    }

    [Fact]
    public void GivenNoKey_WhenConnectionStringRead_ThenOmitsKey()
    {
        var builder = CreateBuilder();
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        var topic = simulator.AddTopic("orders", 60101);

        topic.Resource.ConnectionStringExpression.ValueExpression.ShouldBe(
            "Endpoint={eventgrid.bindings.orders.url}/api/events"
        );
    }

    [Fact]
    public void GivenAKey_WhenConnectionPropertiesRead_ThenExposesUriAndKey()
    {
        var builder = CreateBuilder();
        var key = builder.AddParameter("orders-key", "secret");
        var simulator = builder.AddAzureEventGridSimulator("eventgrid");

        IResourceWithConnectionString topic = simulator.AddTopic("orders", 60101, key).Resource;

        topic.GetConnectionProperties().Select(p => p.Key).ShouldBe(["Uri", "Key"]);
    }
}
