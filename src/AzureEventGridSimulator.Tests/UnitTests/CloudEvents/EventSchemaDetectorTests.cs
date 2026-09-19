using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.CloudEvents;

[Trait("Category", "unit")]
public class EventSchemaDetectorTests
{
    private readonly EventSchemaDetector _detector = new();

    [Fact]
    public void GivenRequestWithJsonContentType_WhenDetected_ThenReturnsEventGridSchema()
    {
        var context = CreateHttpContext("application/json");

        var schema = _detector.DetectSchema(context);

        schema.ShouldBe(EventSchema.EventGridSchema);
    }

    [Fact]
    public void GivenRequestWithCloudEventsContentType_WhenDetected_ThenReturnsCloudEventsSchema()
    {
        var context = CreateHttpContext("application/cloudevents+json");

        var schema = _detector.DetectSchema(context);

        schema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenRequestWithCloudEventsBatchContentType_WhenDetected_ThenReturnsCloudEventsSchema()
    {
        var context = CreateHttpContext("application/cloudevents-batch+json");

        var schema = _detector.DetectSchema(context);

        schema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenRequestWithCloudEventsHeaders_WhenDetected_ThenReturnsCloudEventsSchema()
    {
        var context = CreateHttpContextWithCloudEventsHeaders();

        var schema = _detector.DetectSchema(context);

        schema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenRequestWithPartialCloudEventsHeaders_WhenDetected_ThenReturnsCloudEventsSchema()
    {
        // Azure detects binary mode when ANY ce-* header is present
        // Missing required headers are validated during parsing
        var context = new DefaultHttpContext();
        context.Request.Headers[Constants.CeSpecVersionHeader] = "1.0";
        context.Request.Headers[Constants.CeIdHeader] = "test-id";
        // Missing ce-source and ce-type - but still detected as CloudEvents
        context.Request.ContentType = "application/json";

        var schema = _detector.DetectSchema(context);

        schema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenIsBinaryMode_WhenCloudEventsHeadersPresent_ThenReturnsTrue()
    {
        var context = CreateHttpContextWithCloudEventsHeaders();

        var isBinaryMode = _detector.IsBinaryMode(context);

        isBinaryMode.ShouldBeTrue();
    }

    // Only the four required-attribute headers switch a request into binary mode; optional and
    // extension ce-* headers on their own don't
    [Theory]
    [InlineData(Constants.CeSpecVersionHeader, true)]
    [InlineData(Constants.CeIdHeader, true)]
    [InlineData(Constants.CeSourceHeader, true)]
    [InlineData(Constants.CeTypeHeader, true)]
    [InlineData("CE-TYPE", true)]
    [InlineData(Constants.CeSubjectHeader, false)]
    [InlineData(Constants.CeTimeHeader, false)]
    [InlineData(Constants.CeDataContentTypeHeader, false)]
    [InlineData("ce-myextension", false)]
    public void GivenOneCeHeader_WhenCheckedForBinaryMode_ThenOnlyARequiredAttributeHeaderCounts(
        string header,
        bool expected
    )
    {
        var context = CreateHttpContext("application/json");
        context.Request.Headers[header] = "value";

        _detector.IsBinaryMode(context).ShouldBe(expected);
        _detector
            .DetectSchema(context)
            .ShouldBe(expected ? EventSchema.CloudEventV1_0 : EventSchema.EventGridSchema);
    }

    [Fact]
    public void GivenIsBinaryMode_WhenNoCloudEventsHeaders_ThenReturnsFalse()
    {
        var context = CreateHttpContext("application/json");

        var isBinaryMode = _detector.IsBinaryMode(context);

        isBinaryMode.ShouldBeFalse();
    }

    [Fact]
    public void GivenIsBatchMode_WhenBatchContentType_ThenReturnsTrue()
    {
        var context = CreateHttpContext("application/cloudevents-batch+json");

        var isBatchMode = _detector.IsBatchMode(context);

        isBatchMode.ShouldBeTrue();
    }

    [Fact]
    public void GivenIsBatchMode_WhenSingleCloudEventContentType_ThenReturnsFalse()
    {
        var context = CreateHttpContext("application/cloudevents+json");

        var isBatchMode = _detector.IsBatchMode(context);

        isBatchMode.ShouldBeFalse();
    }

    private static HttpContext CreateHttpContext(string contentType)
    {
        var context = new DefaultHttpContext { Request = { ContentType = contentType } };
        return context;
    }

    private static HttpContext CreateHttpContextWithCloudEventsHeaders()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[Constants.CeSpecVersionHeader] = "1.0";
        context.Request.Headers[Constants.CeIdHeader] = "test-id";
        context.Request.Headers[Constants.CeSourceHeader] = "/test/source";
        context.Request.Headers[Constants.CeTypeHeader] = "com.example.test";
        context.Request.ContentType = "application/json";
        return context;
    }
}
