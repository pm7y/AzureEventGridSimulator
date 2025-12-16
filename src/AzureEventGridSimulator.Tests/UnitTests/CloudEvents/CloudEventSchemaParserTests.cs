using System;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using Microsoft.AspNetCore.Http;
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
            specVersion: "1.0",
            type: "com.example.test",
            source: "/test/source",
            id: "test-id-123"
        );
        var requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].Schema.ShouldBe(EventSchema.CloudEventV1_0);
        events[0].CloudEvent.SpecVersion.ShouldBe("1.0");
        events[0].CloudEvent.Type.ShouldBe("com.example.test");
        events[0].CloudEvent.Source.ShouldBe("/test/source");
        events[0].CloudEvent.Id.ShouldBe("test-id-123");
    }

    [Fact]
    public void GivenBinaryModeRequestWithOptionalHeaders_WhenParsed_ThenOptionalFieldsPopulated()
    {
        var context = CreateBinaryModeContext(
            specVersion: "1.0",
            type: "com.example.test",
            source: "/test/source",
            id: "test-id-123",
            time: "2025-01-15T10:30:00Z",
            subject: "/test/subject",
            dataContentType: "application/json",
            dataSchema: "https://example.com/schema"
        );
        var requestBody = "{\"Property\": \"Value\"}";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.Time.ShouldBe("2025-01-15T10:30:00Z");
        events[0].CloudEvent.Subject.ShouldBe("/test/subject");
        events[0].CloudEvent.DataContentType.ShouldBe("application/json");
        events[0].CloudEvent.DataSchema.ShouldBe("https://example.com/schema");
    }

    [Fact]
    public void GivenBinaryModeRequestWithJsonBody_WhenParsed_ThenDataIsDeserializedObject()
    {
        var context = CreateBinaryModeContext(
            specVersion: "1.0",
            type: "com.example.test",
            source: "/test/source",
            id: "test-id-123"
        );
        var requestBody = "{\"Property\": \"Value\", \"Number\": 42}";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.Data.ShouldNotBeNull();
    }

    [Fact]
    public void GivenBinaryModeRequestWithNonJsonBody_WhenParsed_ThenDataIsString()
    {
        var context = CreateBinaryModeContext(
            specVersion: "1.0",
            type: "com.example.test",
            source: "/test/source",
            id: "test-id-123"
        );
        var requestBody = "plain text data";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.Data.ShouldBe("plain text data");
    }

    [Fact]
    public void GivenBinaryModeRequestWithEmptyBody_WhenParsed_ThenDataIsNull()
    {
        var context = CreateBinaryModeContext(
            specVersion: "1.0",
            type: "com.example.test",
            source: "/test/source",
            id: "test-id-123"
        );
        var requestBody = "";

        var events = _parser.Parse(context, requestBody);

        events[0].CloudEvent.Data.ShouldBeNull();
    }

    [Fact]
    public void GivenStructuredModeRequest_WhenParsed_ThenCloudEventCreatedFromBody()
    {
        var context = CreateStructuredModeContext();
        var requestBody = @"{
            ""specversion"": ""1.0"",
            ""type"": ""com.example.test"",
            ""source"": ""/test/source"",
            ""id"": ""test-id-456""
        }";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].Schema.ShouldBe(EventSchema.CloudEventV1_0);
        events[0].CloudEvent.SpecVersion.ShouldBe("1.0");
        events[0].CloudEvent.Type.ShouldBe("com.example.test");
        events[0].CloudEvent.Source.ShouldBe("/test/source");
        events[0].CloudEvent.Id.ShouldBe("test-id-456");
    }

    [Fact]
    public void GivenStructuredModeRequestWithOptionalFields_WhenParsed_ThenAllFieldsPopulated()
    {
        var context = CreateStructuredModeContext();
        var requestBody = @"{
            ""specversion"": ""1.0"",
            ""type"": ""com.example.test"",
            ""source"": ""/test/source"",
            ""id"": ""test-id-456"",
            ""time"": ""2025-01-15T10:30:00Z"",
            ""subject"": ""/test/subject"",
            ""datacontenttype"": ""application/json"",
            ""dataschema"": ""https://example.com/schema"",
            ""data"": { ""Property"": ""Value"" }
        }";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.Time.ShouldBe("2025-01-15T10:30:00Z");
        events[0].CloudEvent.Subject.ShouldBe("/test/subject");
        events[0].CloudEvent.DataContentType.ShouldBe("application/json");
        events[0].CloudEvent.DataSchema.ShouldBe("https://example.com/schema");
        events[0].CloudEvent.Data.ShouldNotBeNull();
    }

    [Fact]
    public void GivenStructuredModeRequestWithArrayOfOne_WhenParsed_ThenSingleEventReturned()
    {
        var context = CreateStructuredModeContext();
        var requestBody = @"[{
            ""specversion"": ""1.0"",
            ""type"": ""com.example.test"",
            ""source"": ""/test/source"",
            ""id"": ""test-id-789""
        }]";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.Id.ShouldBe("test-id-789");
    }

    [Fact]
    public void GivenStructuredModeRequestWithEmptyBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("empty");
    }

    [Fact]
    public void GivenStructuredModeRequestWithMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "{ invalid json }";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("parse");
    }

    [Fact]
    public void GivenStructuredModeRequestWithEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateStructuredModeContext();
        var requestBody = "[]";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("No events");
    }

    [Fact]
    public void GivenBatchModeRequest_WhenParsed_ThenMultipleEventsReturned()
    {
        var context = CreateBatchModeContext();
        var requestBody = @"[
            {
                ""specversion"": ""1.0"",
                ""type"": ""com.example.test1"",
                ""source"": ""/test/source"",
                ""id"": ""event-1""
            },
            {
                ""specversion"": ""1.0"",
                ""type"": ""com.example.test2"",
                ""source"": ""/test/source"",
                ""id"": ""event-2""
            }
        ]";

        var events = _parser.Parse(context, requestBody);

        events.Length.ShouldBe(2);
        events[0].CloudEvent.Id.ShouldBe("event-1");
        events[0].CloudEvent.Type.ShouldBe("com.example.test1");
        events[1].CloudEvent.Id.ShouldBe("event-2");
        events[1].CloudEvent.Type.ShouldBe("com.example.test2");
    }

    [Fact]
    public void GivenBatchModeRequestWithSingleEvent_WhenParsed_ThenSingleEventReturned()
    {
        var context = CreateBatchModeContext();
        var requestBody = @"[{
            ""specversion"": ""1.0"",
            ""type"": ""com.example.test"",
            ""source"": ""/test/source"",
            ""id"": ""single-event""
        }]";

        var events = _parser.Parse(context, requestBody);

        events.ShouldHaveSingleItem();
        events[0].CloudEvent.Id.ShouldBe("single-event");
    }

    [Fact]
    public void GivenBatchModeRequestWithEmptyBody_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("empty");
    }

    [Fact]
    public void GivenBatchModeRequestWithEmptyArray_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "[]";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("No events");
    }

    [Fact]
    public void GivenBatchModeRequestWithMalformedJson_WhenParsed_ThenExceptionThrown()
    {
        var context = CreateBatchModeContext();
        var requestBody = "[{ invalid }]";

        var exception = Should.Throw<InvalidOperationException>(() => _parser.Parse(context, requestBody));
        exception.Message.ShouldContain("parse");
    }

    [Fact]
    public void GivenValidEvents_WhenValidated_ThenNoExceptionThrown()
    {
        var events = new[]
        {
            SimulatorEvent.FromCloudEvent(new CloudEvent
            {
                SpecVersion = "1.0",
                Type = "com.example.test",
                Source = "/test/source",
                Id = "test-id"
            })
        };

        Should.NotThrow(() => _parser.Validate(events));
    }

    [Fact]
    public void GivenInvalidEvents_WhenValidated_ThenExceptionThrown()
    {
        var events = new[]
        {
            SimulatorEvent.FromCloudEvent(new CloudEvent
            {
                // Missing required fields
                SpecVersion = "1.0"
            })
        };

        Should.Throw<InvalidOperationException>(() => _parser.Validate(events));
    }

    private static HttpContext CreateBinaryModeContext(
        string specVersion,
        string type,
        string source,
        string id,
        string time = null,
        string subject = null,
        string dataContentType = null,
        string dataSchema = null)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";
        context.Request.Headers[Constants.CeSpecVersionHeader] = specVersion;
        context.Request.Headers[Constants.CeTypeHeader] = type;
        context.Request.Headers[Constants.CeSourceHeader] = source;
        context.Request.Headers[Constants.CeIdHeader] = id;

        if (time != null)
            context.Request.Headers[Constants.CeTimeHeader] = time;
        if (subject != null)
            context.Request.Headers[Constants.CeSubjectHeader] = subject;
        if (dataContentType != null)
            context.Request.Headers[Constants.CeDataContentTypeHeader] = dataContentType;
        if (dataSchema != null)
            context.Request.Headers[Constants.CeDataSchemaHeader] = dataSchema;

        return context;
    }

    private static HttpContext CreateStructuredModeContext()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/cloudevents+json";
        return context;
    }

    private static HttpContext CreateBatchModeContext()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/cloudevents-batch+json";
        return context;
    }
}
