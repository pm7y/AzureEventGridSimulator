using System.Globalization;
using System.Text.Json;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class CloudEventSchemaParserTests
{
    private readonly EventSchemaDetector _detector = new();
    private readonly CloudEventSchemaParser _parser;

    public CloudEventSchemaParserTests()
    {
        _parser = new CloudEventSchemaParser(_detector);
    }

    [Fact]
    public void GivenBinaryModeRequest_WhenParsed_ThenCloudEventCreatedFromHeaders()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        const string requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].Schema.ShouldBe(EventSchema.CloudEventV1_0);
        var cloudEvent = events[0].CloudEvent.ShouldNotBeNullAnd();
        cloudEvent.SpecVersion.ShouldBe("1.0");
        cloudEvent.Type.ShouldBe("com.example.test");
        cloudEvent.Source.ShouldBe("/test/source");
        cloudEvent.Id.ShouldBe("test-id-123");
    }

    [Fact]
    public void GivenBinaryModeRequestWithOptionalHeaders_WhenParsed_ThenOptionalFieldsPopulated()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123",
            "2025-01-15T10:30:00Z",
            "/test/subject",
            "application/json",
            "https://example.com/schema"
        );
        const string requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        var cloudEvent = events[0].CloudEvent.ShouldNotBeNullAnd();
        cloudEvent.Time.ShouldBe("2025-01-15T10:30:00Z");
        cloudEvent.Subject.ShouldBe("/test/subject");
        cloudEvent.DataContentType.ShouldBe("application/json");
        cloudEvent.DataSchema.ShouldBe("https://example.com/schema");
    }

    [Fact]
    public void GivenBinaryModeRequestWithJsonBody_WhenParsed_ThenDataIsDeserializedObject()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        const string requestBody = "{\"Property\": \"Value\", \"Number\": 42}";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.ShouldNotBeNullAnd().Data.ShouldNotBeNull();
    }

    [Fact]
    public void GivenBinaryModeRequestWithNonJsonBody_WhenParsed_ThenDataIsString()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        const string requestBody = "plain text data";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.ShouldNotBeNullAnd().Data.ShouldBe("plain text data");
    }

    [Fact]
    public void GivenBinaryModeRequestWithEmptyBody_WhenParsed_ThenDataIsNull()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        const string requestBody = "";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.ShouldNotBeNullAnd().Data.ShouldBeNull();
    }

    [Fact]
    public void GivenStructuredModeRequest_WhenParsed_ThenCloudEventCreatedFromBody()
    {
        var context = CreateStructuredModeContext();
        const string requestBody = """
            {
                        "specversion": "1.0",
                        "type": "com.example.test",
                        "source": "/test/source",
                        "id": "test-id-456"
                    }
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].Schema.ShouldBe(EventSchema.CloudEventV1_0);
        events[0].CloudEvent.ShouldNotBeNullAnd().SpecVersion.ShouldBe("1.0");
        events[0].CloudEvent.ShouldNotBeNullAnd().Type.ShouldBe("com.example.test");
        events[0].CloudEvent.ShouldNotBeNullAnd().Source.ShouldBe("/test/source");
        events[0].CloudEvent.ShouldNotBeNullAnd().Id.ShouldBe("test-id-456");
    }

    [Fact]
    public void GivenStructuredModeRequestWithOptionalFields_WhenParsed_ThenAllFieldsPopulated()
    {
        var context = CreateStructuredModeContext();
        const string requestBody = """
            {
                        "specversion": "1.0",
                        "type": "com.example.test",
                        "source": "/test/source",
                        "id": "test-id-456",
                        "time": "2025-01-15T10:30:00Z",
                        "subject": "/test/subject",
                        "datacontenttype": "application/json",
                        "dataschema": "https://example.com/schema",
                        "data": { "Property": "Value" }
                    }
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.ShouldNotBeNullAnd().Time.ShouldBe("2025-01-15T10:30:00Z");
        events[0].CloudEvent.ShouldNotBeNullAnd().Subject.ShouldBe("/test/subject");
        events[0].CloudEvent.ShouldNotBeNullAnd().DataContentType.ShouldBe("application/json");
        events[0].CloudEvent.ShouldNotBeNullAnd().DataSchema.ShouldBe("https://example.com/schema");
        events[0].CloudEvent.ShouldNotBeNullAnd().Data.ShouldNotBeNull();
    }

    [Fact]
    public void GivenStructuredModeRequestWithArrayOfOne_WhenParsed_ThenSingleEventReturned()
    {
        var context = CreateStructuredModeContext();
        var requestBody = """
            [{
                        "specversion": "1.0",
                        "type": "com.example.test",
                        "source": "/test/source",
                        "id": "test-id-789"
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.ShouldNotBeNullAnd().Id.ShouldBe("test-id-789");
    }

    [Fact]
    public void GivenStructuredModeRequestWithEmptyBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenStructuredModeRequestWithMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "{ invalid json }";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        // The parser passes System.Text.Json's message through unchanged and adds no report
        // suffix; EventGridMiddleware appends the suffix to the response, as Azure does.
        exception.Message.ShouldNotContain("to our forums");
        exception.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public void GivenStructuredModeRequestWithEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "[]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("configured to receive event");
    }

    [Fact]
    public void GivenBatchModeRequest_WhenParsed_ThenMultipleEventsReturned()
    {
        var context = CreateBatchModeContext();
        var requestBody = """
            [
                        {
                            "specversion": "1.0",
                            "type": "com.example.test1",
                            "source": "/test/source",
                            "id": "event-1"
                        },
                        {
                            "specversion": "1.0",
                            "type": "com.example.test2",
                            "source": "/test/source",
                            "id": "event-2"
                        }
                    ]
            """;

        var events = _parser.Parse(context, requestBody);

        events.Length.ShouldBe(2);
        events[0].CloudEvent.ShouldNotBeNullAnd().Id.ShouldBe("event-1");
        events[0].CloudEvent.ShouldNotBeNullAnd().Type.ShouldBe("com.example.test1");
        events[1].CloudEvent.ShouldNotBeNullAnd().Id.ShouldBe("event-2");
        events[1].CloudEvent.ShouldNotBeNullAnd().Type.ShouldBe("com.example.test2");
    }

    [Fact]
    public void GivenBatchModeRequestWithSingleEvent_WhenParsed_ThenSingleEventReturned()
    {
        var context = CreateBatchModeContext();
        var requestBody = """
            [{
                        "specversion": "1.0",
                        "type": "com.example.test",
                        "source": "/test/source",
                        "id": "single-event"
                    }]
            """;

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.ShouldNotBeNullAnd().Id.ShouldBe("single-event");
    }

    [Fact]
    public void GivenBatchModeRequestWithEmptyBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("Unexpected end when reading JSON");
    }

    [Fact]
    public void GivenBatchModeRequestWithEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "[]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        exception.Message.ShouldContain("configured to receive event");
    }

    [Fact]
    public void GivenBatchModeRequestWithMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "[{ invalid }]";

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );
        // The parser passes System.Text.Json's message through unchanged and adds no report
        // suffix; EventGridMiddleware appends the suffix to the response, as Azure does.
        exception.Message.ShouldNotContain("to our forums");
        exception.InnerException.ShouldBeAssignableTo<JsonException>();
    }

    [Fact]
    public void GivenValidEvents_WhenValidated_ThenNoExceptionThrown()
    {
        var events = new[]
        {
            SimulatorEvent.FromCloudEvent(
                new CloudEvent
                {
                    SpecVersion = "1.0",
                    Type = "com.example.test",
                    Source = "/test/source",
                    Id = "test-id",
                }
            ),
        };

        Should.NotThrow(() => _parser.Validate(events));
    }

    // System.Text.Json reports the missing required properties (type and id); the parser names
    // one of them in Azure's order and shows 'type' as 'eventType', as Azure does
    [Theory]
    [InlineData(new[] { "type" }, "eventType")]
    [InlineData(new[] { "id" }, "id")]
    [InlineData(new[] { "type", "id" }, "eventType")]
    public void GivenCloudEventJsonWithMissingRequiredFields_WhenParsed_ThenMessageNamesTheFieldAzureReports(
        string[] missingFields,
        string expectedField
    )
    {
        var context = CreateStructuredModeContext();
        var evt = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["specversion"] = "1.0",
            ["type"] = "com.example.test",
            ["source"] = "/test/source",
            ["id"] = "test-id",
        };
        foreach (var field in missingFields)
        {
            evt.Remove(field);
        }

        var requestBody = JsonSerializer.Serialize(evt);

        // STJ joins the missing names with the UI culture's list separator, and the parser splits
        // on ','
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        InvalidOperationException exception;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

            exception = Should.Throw<InvalidOperationException>(() =>
                _parser.Parse(context, requestBody)
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }

        exception.Message.ShouldStartWith(
            $"This resource is configured for 'CloudEventV10' schema and requires '{expectedField}' property to be set."
        );
    }

    [Fact]
    public void GivenApplicationJsonArray_WhenParsed_ThenRejectedAsNotConformingToSingleEventSchema()
    {
        // With application/json, Azure expects a single CloudEvent object, not an array
        var context = new DefaultHttpContext { Request = { ContentType = "application/json" } };
        const string requestBody = """
            [{ "specversion": "1.0", "type": "com.example.test", "source": "/test/source", "id": "abc-1" }]
            """;

        var exception = Should.Throw<InvalidOperationException>(() =>
            _parser.Parse(context, requestBody)
        );

        exception.Message.ShouldStartWith(
            "This resource is configured to receive event in 'CloudEventV10' schema. "
                + "The JSON received does not conform to the expected schema. "
                + "Token Expected: StartObject, Actual Token Received: StartArray."
        );
    }

    [Fact]
    public void GivenBinaryModeRequestWithPercentEncodedHeaders_WhenParsed_ThenValuesAreDecoded()
    {
        // CloudEvents HTTP binding requires percent-encoding for spaces and non-ASCII characters
        var context = CreateBinaryModeContextWithRawHeaders(
            "1.0",
            "com.example.test",
            "/test/source%20with%20spaces", // Space encoded as %20
            "test-id-123",
            "Euro%20%E2%82%AC" // "Euro €" percent-encoded
        );
        var requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.ShouldNotBeNullAnd().Source.ShouldBe("/test/source with spaces");
        events[0].CloudEvent.ShouldNotBeNullAnd().Subject.ShouldBe("Euro €");
    }

    [Fact]
    public void GivenBinaryModeRequestWithNonEncodedHeaders_WhenParsed_ThenValuesPassThrough()
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        var requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.ShouldNotBeNullAnd().Source.ShouldBe("/test/source");
    }

    [Theory]
    [InlineData(Constants.CeSpecVersionHeader)]
    [InlineData(Constants.CeIdHeader)]
    [InlineData(Constants.CeSourceHeader)]
    [InlineData(Constants.CeTypeHeader)]
    public void GivenBinaryModeRequestWithoutARequiredHeader_WhenParsed_ThenMessageNamesTheMissingHeader(
        string header
    )
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        context.Request.Headers.Remove(header);

        var exception = Should.Throw<EventParseException>(() => _parser.Parse(context, "{}"));

        exception.ErrorCode.ShouldBe(ErrorDetailCodes.InvalidCloudEventHeader);
        exception.Message.ShouldBe(
            $"{header} header is missing for the cloud event. "
                + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes"
        );
    }

    [Theory]
    [InlineData(Constants.CeSpecVersionHeader)]
    [InlineData(Constants.CeIdHeader)]
    [InlineData(Constants.CeSourceHeader)]
    [InlineData(Constants.CeTypeHeader)]
    public void GivenBinaryModeRequestWithAnEmptyRequiredHeader_WhenParsed_ThenMessageNamesTheEmptyHeader(
        string header
    )
    {
        var context = CreateBinaryModeContext(
            "1.0",
            "com.example.test",
            "/test/source",
            "test-id-123"
        );
        context.Request.Headers[header] = "";

        var exception = Should.Throw<EventParseException>(() => _parser.Parse(context, "{}"));

        exception.ErrorCode.ShouldBe(ErrorDetailCodes.InvalidCloudEventHeader);
        exception.Message.ShouldBe(
            $"{header} header is empty for the cloud event. "
                + "Please check required attributes at https://github.com/cloudevents/spec/blob/v1.0/spec.md#required-attributes"
        );
    }

    private static DefaultHttpContext CreateBinaryModeContext(
        string specVersion,
        string type,
        string source,
        string id,
        string? time = null,
        string? subject = null,
        string? dataContentType = null,
        string? dataSchema = null
    )
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = "application/json",
                Headers =
                {
                    [Constants.CeSpecVersionHeader] = specVersion,
                    [Constants.CeTypeHeader] = type,
                    [Constants.CeSourceHeader] = source,
                    [Constants.CeIdHeader] = id,
                },
            },
        };

        if (time != null)
        {
            context.Request.Headers[Constants.CeTimeHeader] = time;
        }

        if (subject != null)
        {
            context.Request.Headers[Constants.CeSubjectHeader] = subject;
        }

        if (dataContentType != null)
        {
            context.Request.Headers[Constants.CeDataContentTypeHeader] = dataContentType;
        }

        if (dataSchema != null)
        {
            context.Request.Headers[Constants.CeDataSchemaHeader] = dataSchema;
        }

        return context;
    }

    private static DefaultHttpContext CreateStructuredModeContext()
    {
        var context = new DefaultHttpContext
        {
            Request = { ContentType = "application/cloudevents+json" },
        };
        return context;
    }

    private static DefaultHttpContext CreateBatchModeContext()
    {
        var context = new DefaultHttpContext
        {
            Request = { ContentType = "application/cloudevents-batch+json" },
        };
        return context;
    }

    private static DefaultHttpContext CreateBinaryModeContextWithRawHeaders(
        string specVersion,
        string type,
        string source,
        string id,
        string? subject = null
    )
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                ContentType = "application/json",
                Headers =
                {
                    [Constants.CeSpecVersionHeader] = specVersion,
                    [Constants.CeTypeHeader] = type,
                    [Constants.CeSourceHeader] = source,
                    [Constants.CeIdHeader] = id,
                },
            },
        };

        if (subject != null)
        {
            context.Request.Headers[Constants.CeSubjectHeader] = subject;
        }

        return context;
    }
}
