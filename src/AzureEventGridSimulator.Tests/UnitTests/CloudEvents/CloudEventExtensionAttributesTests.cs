using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

/// <summary>
///     Tests that CloudEvents extension attributes (unknown top-level attributes) are preserved
///     through the simulator pipeline: ingestion, storage and delivery to subscribers.
///     This mirrors Azure Event Grid, which passes extension context attributes through unchanged.
/// </summary>
[Trait("Category", "unit")]
public class CloudEventExtensionAttributesTests
{
    private readonly EventSchemaDetector _detector = new();
    private readonly CloudEventSchemaParser _parser;
    private readonly CloudEventSchemaFormatter _formatter = new();

    public CloudEventExtensionAttributesTests()
    {
        _parser = new CloudEventSchemaParser(_detector);
    }

    [Fact]
    public void GivenCloudEventWithExtensionAttribute_WhenSerialized_ThenAttributeIsReEmitted()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            ExtensionAttributes = new Dictionary<string, JsonElement>
            {
                ["claimcheckurl"] = JsonSerializer.SerializeToElement(
                    "http://localhost/some-payload.json"
                ),
            },
        };

        var simulatorEvent = SimulatorEvent.FromCloudEvent(cloudEvent);
        var json = _formatter.SerializeSingle(simulatorEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("claimcheckurl")
            .GetString()
            .ShouldBe("http://localhost/some-payload.json");
    }

    [Fact]
    public void GivenStructuredModeRequestWithExtensionAttribute_WhenParsed_ThenAttributeIsCaptured()
    {
        var context = CreateStructuredModeContext();
        const string requestBody = """
            {
                "specversion": "1.0",
                "type": "com.example.test",
                "source": "/test/source",
                "id": "test-id-456",
                "claimcheckurl": "http://localhost/payload.json"
            }
            """;

        var events = _parser.Parse(context, requestBody);

        var cloudEvent = events.ShouldHaveSingleItem().CloudEvent.ShouldNotBeNullAnd();
        cloudEvent.ExtensionAttributes.ShouldNotBeNull();
        cloudEvent
            .ExtensionAttributes!["claimcheckurl"]
            .GetString()
            .ShouldBe("http://localhost/payload.json");
    }

    [Fact]
    public void GivenBinaryModeRequestWithExtensionHeader_WhenParsed_ThenAttributeIsCaptured()
    {
        // In binary mode, extension attributes arrive as ce-<name> HTTP headers.
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = "application/json",
                Headers =
                {
                    [Constants.CeSpecVersionHeader] = "1.0",
                    [Constants.CeTypeHeader] = "com.example.test",
                    [Constants.CeSourceHeader] = "/test/source",
                    [Constants.CeIdHeader] = "test-id-123",
                    ["ce-claimcheckurl"] = "http://localhost/payload.json",
                },
            },
        };

        var events = _parser.Parse(context, "{\"Property\": \"Value\"}");

        var cloudEvent = events.ShouldHaveSingleItem().CloudEvent.ShouldNotBeNullAnd();
        cloudEvent.ExtensionAttributes.ShouldNotBeNull();
        cloudEvent
            .ExtensionAttributes!["claimcheckurl"]
            .GetString()
            .ShouldBe("http://localhost/payload.json");
    }

    [Fact]
    public void GivenBinaryModeRequestWithEmptyExtensionHeader_WhenParsed_ThenAttributeIsPreserved()
    {
        // A present-but-empty ce-<name> header is still an extension attribute and must be
        // preserved (matching GetHeaderValue, which keeps empty strings rather than dropping them).
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = "application/json",
                Headers =
                {
                    [Constants.CeSpecVersionHeader] = "1.0",
                    [Constants.CeTypeHeader] = "com.example.test",
                    [Constants.CeSourceHeader] = "/test/source",
                    [Constants.CeIdHeader] = "test-id-123",
                    ["ce-emptyext"] = "",
                },
            },
        };

        var events = _parser.Parse(context, "{\"Property\": \"Value\"}");

        var cloudEvent = events.ShouldHaveSingleItem().CloudEvent.ShouldNotBeNullAnd();
        cloudEvent.ExtensionAttributes.ShouldNotBeNull();
        cloudEvent.ExtensionAttributes!.ShouldContainKey("emptyext");
        cloudEvent.ExtensionAttributes["emptyext"].GetString().ShouldBe("");
    }

    [Fact]
    public void GivenPublishedEventWithExtensionAttribute_WhenParsedAndDelivered_ThenAttributeSurvives()
    {
        // End-to-end: an extension attribute published to the simulator must reach subscribers,
        // matching Azure Event Grid's pass-through behaviour. Delivery uses the batch array form.
        var context = CreateStructuredModeContext();
        const string requestBody = """
            {
                "specversion": "1.0",
                "type": "com.example.test",
                "source": "/test/source",
                "id": "test-id-456",
                "claimcheckurl": "http://localhost/payload.json"
            }
            """;

        var events = _parser.Parse(context, requestBody);
        var json = _formatter.Serialize(events.ShouldHaveSingleItem());

        using var doc = JsonDocument.Parse(json);
        var delivered = doc.RootElement;
        delivered.GetArrayLength().ShouldBe(1);
        delivered[0]
            .GetProperty("claimcheckurl")
            .GetString()
            .ShouldBe("http://localhost/payload.json");
    }

    private static DefaultHttpContext CreateStructuredModeContext()
    {
        return new DefaultHttpContext
        {
            Request = { ContentType = "application/cloudevents+json" },
        };
    }
}
