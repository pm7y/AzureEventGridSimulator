using System.Net;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Validation;

[Trait("Category", "unit")]
public class RequestBodyValidatorTests
{
    private readonly RequestBodyValidator _validator = new(
        Substitute.For<ILogger<RequestBodyValidator>>()
    );

    private static EventValidationLimits CreateLimits(
        int overallBytes = 1536000,
        int perEventBytes = 1049600
    )
    {
        return new EventValidationLimits
        {
            MaximumOverallMessageSizeInBytes = overallBytes,
            MaximumEventSizeInBytes = perEventBytes,
        };
    }

    [Fact]
    public void GivenMultiByteBody_WhenCharCountIsUnderLimitButUtf8ByteCountIsOver_ThenRejectedWith413()
    {
        // 60 chars of '€' (3 bytes each in UTF-8) = 60 chars but 180 bytes.
        // The limit is in bytes, so this must be rejected even though the char count is under.
        var body = new string('€', 60);
        var context = TestHelpers.CreateHttpContext();

        var result = _validator.ValidateRequestBody(context, body, CreateLimits(overallBytes: 100));

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        result.FailureReason.ShouldBe(BodyValidationFailureReason.OverallSizeTooLarge);
    }

    [Fact]
    public void GivenBodyExactlyAtByteLimit_WhenValidated_ThenAccepted()
    {
        var body = new string('a', 100);
        var context = TestHelpers.CreateHttpContext();

        var result = _validator.ValidateRequestBody(context, body, CreateLimits(overallBytes: 100));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void GivenBodyOneByteOverLimit_WhenValidated_ThenRejectedWith413()
    {
        var body = new string('a', 101);
        var context = TestHelpers.CreateHttpContext();

        var result = _validator.ValidateRequestBody(context, body, CreateLimits(overallBytes: 100));

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        result.ErrorMessage.ShouldNotBeNullAnd().ShouldContain("maximum size");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenEmptyOrWhitespaceBodyInBinaryMode_WhenValidated_ThenRejectedWith400(
        string body
    )
    {
        var context = TestHelpers.CreateCloudEventsBinaryModeContext();

        var result = _validator.ValidateRequestBody(context, body, CreateLimits());

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.FailureReason.ShouldBe(BodyValidationFailureReason.EmptyBodyInBinaryMode);
    }

    [Fact]
    public void GivenSingleCeHeaderOnly_WhenBodyIsEmpty_ThenStillTreatedAsBinaryMode()
    {
        // Any single ce-* header is enough to put the request in binary mode
        var context = new DefaultHttpContext();
        context.Request.Headers[Constants.CeIdHeader] = "some-id";

        var result = _validator.ValidateRequestBody(context, "", CreateLimits());

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(BodyValidationFailureReason.EmptyBodyInBinaryMode);
    }

    [Fact]
    public void GivenEmptyBodyWithoutBinaryHeaders_WhenValidated_ThenBodyValidationPasses()
    {
        // An empty body without ce-* headers is left for the parser to reject
        var context = TestHelpers.CreateHttpContext();

        var result = _validator.ValidateRequestBody(context, "", CreateLimits());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void GivenEventGridEventUnderPerEventLimit_WhenValidated_ThenAccepted()
    {
        var events = new[] { TestHelpers.CreateSimulatorEventFromEventGrid() };

        var result = _validator.ValidateEventSizes(events, CreateLimits());

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void GivenEventGridEventOverPerEventLimit_WhenValidated_ThenRejectedWith413()
    {
        var events = new[]
        {
            TestHelpers.CreateSimulatorEventFromEventGrid(data: new string('x', 1000)),
        };

        var result = _validator.ValidateEventSizes(events, CreateLimits(perEventBytes: 500));

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        result.FailureReason.ShouldBe(BodyValidationFailureReason.IndividualEventTooLarge);
    }

    [Fact]
    public void GivenMultipleEvents_WhenOnlyOneIsOversized_ThenRejected()
    {
        var events = new[]
        {
            TestHelpers.CreateSimulatorEventFromEventGrid(id: "small-event"),
            TestHelpers.CreateSimulatorEventFromEventGrid(
                id: "big-event",
                data: new string('x', 1000)
            ),
        };

        var result = _validator.ValidateEventSizes(events, CreateLimits(perEventBytes: 500));

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(BodyValidationFailureReason.IndividualEventTooLarge);
    }

    [Fact]
    public void GivenCloudEventOverPerEventLimit_WhenValidated_ThenRejectedWith413()
    {
        var events = new[]
        {
            TestHelpers.CreateSimulatorEventFromCloudEvent(data: new string('x', 1000)),
        };

        var result = _validator.ValidateEventSizes(events, CreateLimits(perEventBytes: 500));

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        result.FailureReason.ShouldBe(BodyValidationFailureReason.IndividualEventTooLarge);
    }
}
