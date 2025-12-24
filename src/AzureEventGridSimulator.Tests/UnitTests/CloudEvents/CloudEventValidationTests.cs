using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class CloudEventValidationTests
{
    [Fact]
    public void GivenValidCloudEvent_WhenValidated_ThenNoExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenValidCloudEventWithOptionalFields_WhenValidated_ThenNoExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Time = "2025-01-15T10:30:00Z",
            Subject = "/test/subject",
            DataContentType = "application/json",
            DataSchema = "https://example.com/schema",
            Data = new { Property = "Value" },
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenCloudEventJsonWithMissingSpecVersion_WhenDeserialized_ThenExceptionThrown()
    {
        const string json = """
            {
                "type": "com.example.test",
                "source": "/test/source",
                "id": "test-id-123"
            }
            """;

        var exception = Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CloudEvent>(json)
        );
        exception.Message.ShouldContain("specversion");
    }

    [Theory]
    [InlineData("0.3")]
    [InlineData("2.0")]
    [InlineData("invalid")]
    public void GivenCloudEventWithAnySpecVersion_WhenValidated_ThenNoExceptionThrown(
        string specVersion
    )
    {
        // Azure Event Grid does not validate specversion value - it accepts any value
        var cloudEvent = new CloudEvent
        {
            SpecVersion = specVersion,
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenCloudEventJsonWithMissingType_WhenDeserialized_ThenExceptionThrown()
    {
        const string json = """
            {
                "specversion": "1.0",
                "source": "/test/source",
                "id": "test-id-123"
            }
            """;

        var exception = Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CloudEvent>(json)
        );
        exception.Message.ShouldContain("type");
    }

    [Fact]
    public void GivenCloudEventJsonWithMissingSource_WhenDeserialized_ThenExceptionThrown()
    {
        const string json = """
            {
                "specversion": "1.0",
                "type": "com.example.test",
                "id": "test-id-123"
            }
            """;

        var exception = Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CloudEvent>(json)
        );
        exception.Message.ShouldContain("source");
    }

    [Fact]
    public void GivenCloudEventJsonWithMissingId_WhenDeserialized_ThenExceptionThrown()
    {
        const string json = """
            {
                "specversion": "1.0",
                "type": "com.example.test",
                "source": "/test/source"
            }
            """;

        var exception = Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<CloudEvent>(json)
        );
        exception.Message.ShouldContain("id");
    }

    [Theory]
    [InlineData("not-a-valid-timestamp")]
    [InlineData("2025-01-15 10:30:00")] // Missing timezone
    public void GivenCloudEventWithInvalidTime_WhenValidated_ThenExceptionThrown(string time)
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Time = time,
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain(
            "The event time property 'time' was not a valid date/time."
        );
    }

    [Fact]
    public void GivenCloudEventWithBothDataAndDataBase64_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "test-id-123",
            Data = new { Property = "Value" },
            DataBase64 = "SGVsbG8gV29ybGQ=",
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("mutually exclusive");
    }

    [Theory]
    [InlineData("/relative/path")]
    [InlineData("https://example.com/absolute")]
    [InlineData("urn:uuid:6e8bc430-9c3a-11d9-9669-0800200c9a66")]
    [InlineData("/")]
    [InlineData("//authority/path")]
    public void GivenCloudEventWithValidUriReferenceSource_WhenValidated_ThenNoExceptionThrown(
        string source
    )
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = source,
            Id = "test-id-123",
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }

    [Fact]
    public void GivenCloudEventWithEmptyId_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "",
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("'id'");
        exception.Message.ShouldContain("CloudEventV10");
    }

    [Fact]
    public void GivenCloudEventWithWhitespaceId_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = "/test/source",
            Id = "   ",
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("'id'");
    }

    [Fact]
    public void GivenCloudEventWithEmptyType_WhenValidated_ThenExceptionThrown()
    {
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "",
            Source = "/test/source",
            Id = "test-id-123",
        };

        var exception = Should.Throw<InvalidOperationException>(() => cloudEvent.Validate());
        exception.Message.ShouldContain("'eventType'");
        exception.Message.ShouldContain("CloudEventV10");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenCloudEventWithEmptySource_WhenValidated_ThenNoExceptionThrown(string source)
    {
        // Azure Event Grid does not validate source value - it accepts any value
        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.0",
            Type = "com.example.test",
            Source = source,
            Id = "test-id-123",
        };

        Should.NotThrow(() => cloudEvent.Validate());
    }
}
