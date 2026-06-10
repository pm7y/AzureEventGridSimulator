using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.EventGrid;

/// <summary>
///     Tests for CloudEvent-to-EventGridEvent conversion fidelity:
///     data_base64 pass-through, dataschema version extraction (including
///     relative URIs which used to crash) and null-field omission.
/// </summary>
[Trait("Category", "unit")]
public class EventGridSchemaFormatterConversionTests
{
    private readonly EventGridSchemaFormatter _formatter = new(TimeProvider.System);

    private string ConvertToJson(CloudEvent cloudEvent)
    {
        return _formatter.SerializeSingle(SimulatorEvent.FromCloudEvent(cloudEvent));
    }

    [Fact]
    public void GivenCloudEventWithOnlyDataBase64_WhenConverted_ThenBinaryPayloadIsPreserved()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent();
        cloudEvent.DataBase64 = "SGVsbG8gd29ybGQ=";

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data").GetString().ShouldBe("SGVsbG8gd29ybGQ=");
    }

    [Fact]
    public void GivenCloudEventWithBothDataAndDataBase64_WhenConverted_ThenDataTakesPrecedence()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent(data: new { Property = "Value" });
        cloudEvent.DataBase64 = "SGVsbG8=";

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("data").GetProperty("Property").GetString().ShouldBe("Value");
    }

    [Fact]
    public void GivenCloudEventWithRelativeDataSchema_WhenConverted_ThenDoesNotThrowAndExtractsVersion()
    {
        // Relative dataschema URIs used to crash version extraction (Uri.Segments
        // throws InvalidOperationException on non-absolute URIs)
        var cloudEvent = TestHelpers.CreateValidCloudEvent();
        cloudEvent.DataSchema = "#/schema/v1";

        string? json = null;
        Should.NotThrow(() => json = ConvertToJson(cloudEvent));

        using var doc = JsonDocument.Parse(json.ShouldNotBeNullAnd());
        doc.RootElement.GetProperty("dataVersion").GetString().ShouldBe("v1");
    }

    [Fact]
    public void GivenCloudEventWithRelativeDataSchemaWithoutVersionSegment_WhenConverted_ThenFullSchemaUsed()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent();
        cloudEvent.DataSchema = "#/schema/customer";

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("dataVersion").GetString().ShouldBe("#/schema/customer");
    }

    [Fact]
    public void GivenCloudEventWithAbsoluteDataSchemaEndingInVersion_WhenConverted_ThenVersionExtracted()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent();
        cloudEvent.DataSchema = "https://example.com/schema/v2";

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("dataVersion").GetString().ShouldBe("v2");
    }

    [Fact]
    public void GivenCloudEventWithAbsoluteDataSchemaWithoutVersionSegment_WhenConverted_ThenFullSchemaUsed()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent();
        cloudEvent.DataSchema = "https://example.com/schemas/customer";

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("dataVersion")
            .GetString()
            .ShouldBe("https://example.com/schemas/customer");
    }

    [Fact]
    public void GivenCloudEventWithoutDataSchema_WhenConverted_ThenDataVersionIsEmpty()
    {
        var cloudEvent = TestHelpers.CreateValidCloudEvent();

        var json = ConvertToJson(cloudEvent);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("dataVersion").GetString().ShouldBe("");
    }

    [Fact]
    public void GivenConvertedCloudEvent_WhenSerialized_ThenMetadataVersionIsOne()
    {
        var json = ConvertToJson(TestHelpers.CreateValidCloudEvent());

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("metadataVersion").GetString().ShouldBe("1");
    }

    [Fact]
    public void GivenEventGridEventWithNullData_WhenSerialized_ThenNullFieldsAreOmitted()
    {
        // Azure omits absent fields rather than emitting them as null
        var evt = new EventGridEvent
        {
            Id = "event-123",
            Subject = "/test/subject",
            EventType = "Test.Event.Type",
            EventTime = "2025-01-15T10:30:00Z",
            DataVersion = "1.0",
            Data = null,
        };

        var json = _formatter.SerializeSingle(SimulatorEvent.FromEventGridEvent(evt));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("data", out _).ShouldBeFalse();
    }
}
