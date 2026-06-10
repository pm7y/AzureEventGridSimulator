using System.Net;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Validation;

[Trait("Category", "unit")]
public class ContentTypeValidatorTests
{
    private readonly ContentTypeValidator _validator = new(
        Substitute.For<ILogger<ContentTypeValidator>>()
    );

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData(null)]
    public void GivenEventGridSchema_WhenAnyContentType_ThenValid(string? contentType)
    {
        // Azure is lenient about content types for the EventGrid schema
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;

        var result = _validator.ValidateContentType(context, EventSchema.EventGridSchema);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("application/cloudevents+json")]
    [InlineData("application/cloudevents+json; charset=utf-8")]
    [InlineData("application/cloudevents-batch+json")]
    [InlineData("application/json")]
    public void GivenStructuredCloudEvent_WhenContentTypeIsAcceptable_ThenValid(string contentType)
    {
        var context = TestHelpers.CreateHttpContext(contentType);

        var result = _validator.ValidateContentType(context, EventSchema.CloudEventV1_0);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public void GivenStructuredCloudEvent_WhenContentTypeIsInvalidOrMissing_ThenRejectedWith415(
        string? contentType
    )
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = contentType;

        var result = _validator.ValidateContentType(context, EventSchema.CloudEventV1_0);

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("Content-Type header");
    }

    [Theory]
    [InlineData("application/cloudevents+json")]
    [InlineData("application/cloudevents-batch+json; charset=utf-8")]
    public void GivenBinaryModeHeaders_WhenContentTypeIsStructured_ThenConflictRejectedWith400(
        string contentType
    )
    {
        // ce-* headers (binary mode) combined with a structured CloudEvents
        // content type is a content-mode conflict - Azure returns 400
        var context = TestHelpers.CreateCloudEventsBinaryModeContext();
        context.Request.ContentType = contentType;

        var result = _validator.ValidateContentType(context, EventSchema.CloudEventV1_0);

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("Conflicting content mode");
    }

    [Fact]
    public void GivenBinaryModeHeaders_WhenContentTypeIsApplicationJson_ThenValid()
    {
        var context = TestHelpers.CreateCloudEventsBinaryModeContext();
        context.Request.ContentType = "application/json";

        var result = _validator.ValidateContentType(context, EventSchema.CloudEventV1_0);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public void GivenBinaryModeHeaders_WhenContentTypeIsNotJson_ThenRejectedWith415(
        string? contentType
    )
    {
        var context = TestHelpers.CreateCloudEventsBinaryModeContext();
        context.Request.ContentType = contentType;

        var result = _validator.ValidateContentType(context, EventSchema.CloudEventV1_0);

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }
}
