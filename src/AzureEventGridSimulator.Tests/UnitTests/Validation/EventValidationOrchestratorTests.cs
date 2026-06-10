using System.Net;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Services;
using AzureEventGridSimulator.Domain.Services.Validation;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using NSubstitute;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Validation;

[Trait("Category", "unit")]
public class EventValidationOrchestratorTests
{
    private static EventValidationOrchestrator CreateOrchestrator(
        SimulatorSettings? settings = null
    )
    {
        var detector = new EventSchemaDetector();
        return new EventValidationOrchestrator(
            detector,
            new EventSchemaParserFactory(
                new EventGridSchemaParser(),
                new CloudEventSchemaParser(detector)
            ),
            new RequestBodyValidator(Substitute.For<ILogger<RequestBodyValidator>>()),
            new ContentTypeValidator(Substitute.For<ILogger<ContentTypeValidator>>()),
            settings ?? new SimulatorSettings(),
            Substitute.For<ILogger<EventValidationOrchestrator>>()
        );
    }

    private static SimulatorSettings CreateSettingsWithLimits(
        int overallBytes = 1536000,
        int perEventBytes = 1049600
    )
    {
        return new SimulatorSettings
        {
            EventValidationLimits = new EventValidationLimits
            {
                MaximumOverallMessageSizeInBytes = overallBytes,
                MaximumEventSizeInBytes = perEventBytes,
            },
        };
    }

    private static string SerializeEvents(params EventGridEvent[] events)
    {
        return JsonSerializer.Serialize(events);
    }

    [Fact]
    public async Task GivenValidEventGridArray_WhenValidated_ThenSucceedsWithEventGridSchema()
    {
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();
        var body = SerializeEvents(TestHelpers.CreateValidEventGridEvent());

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeTrue();
        result.Events.ShouldNotBeNullAnd().Length.ShouldBe(1);
        result.DetectedSchema.ShouldBe(EventSchema.EventGridSchema);
    }

    [Fact]
    public async Task GivenValidSingleEventGridObject_WhenValidated_ThenSucceeds()
    {
        // Azure accepts a single object as well as an array for the EventGrid schema
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();
        var body = JsonSerializer.Serialize(TestHelpers.CreateValidEventGridEvent());

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeTrue();
        result.Events.ShouldNotBeNullAnd().Length.ShouldBe(1);
    }

    [Fact]
    public async Task GivenOversizedBody_WhenValidated_ThenFailsAtBodySizeStageBeforeParsing()
    {
        // The body is deliberately not valid JSON; failing at the BodySize stage
        // (rather than Parsing) proves the size check short-circuits the pipeline.
        var orchestrator = CreateOrchestrator(CreateSettingsWithLimits(overallBytes: 50));
        var context = TestHelpers.CreateHttpContext();
        var body = new string('x', 100);

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.BodySize);
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task GivenMalformedJson_WhenValidated_ThenFailsAtParsingStageWith400()
    {
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            "{ this is not json"
        );

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.Parsing);
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenEmptyJsonArray_WhenValidated_ThenFailsWith400AndSchemaMessage()
    {
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            "[]"
        );

        result.IsValid.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result
            .ErrorMessage.ShouldNotBeNullAnd()
            .ShouldContain("does not conform to the expected schema");
    }

    [Fact]
    public async Task GivenPublisherSetTopic_WhenValidated_ThenFailsWith401AtEventValidationStage()
    {
        // Azure returns 401 when the publisher sets the topic property themselves
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();
        var evt = TestHelpers.CreateValidEventGridEvent();
        evt.SetTopic("/publisher/should/not/set/this");
        var body = SerializeEvents(evt);

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.EventValidation);
        result.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GivenEventExceedingPerEventLimit_WhenValidated_ThenFailsAtEventSizeStage()
    {
        var orchestrator = CreateOrchestrator(CreateSettingsWithLimits(perEventBytes: 50));
        var context = TestHelpers.CreateHttpContext();
        var body = SerializeEvents(TestHelpers.CreateValidEventGridEvent());

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.EventSize);
        result.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task GivenBinaryHeadersWithStructuredContentType_WhenValidated_ThenFailsAtContentTypeStage()
    {
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateCloudEventsBinaryModeContext();
        context.Request.ContentType = "application/cloudevents+json";

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            "{}"
        );

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.ContentType);
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenTopicInputSchemaOverride_WhenBodyDoesNotMatchSchema_ThenFailsAtParsing()
    {
        // The topic forces CloudEvents input; an array body with application/json
        // content type is invalid for CloudEvents single-event mode.
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateHttpContext();
        var topic = new TopicSettings
        {
            Name = "TestTopic",
            Port = 60101,
            Key = "TheLocal+DevelopmentKey=",
            InputSchema = EventSchema.CloudEventV1_0,
        };
        const string body = """
            [{ "specversion": "1.0", "type": "com.example.test", "source": "/test/source", "id": "abc-1" }]
            """;

        var result = await orchestrator.ValidateEvents(context, topic, body);

        result.IsValid.ShouldBeFalse();
        result.FailureStage.ShouldBe(ValidationFailureStage.Parsing);
        result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenValidStructuredCloudEvent_WhenValidated_ThenSucceedsWithCloudEventSchema()
    {
        var orchestrator = CreateOrchestrator();
        var context = TestHelpers.CreateCloudEventsStructuredModeContext();
        const string body = """
            { "specversion": "1.0", "type": "com.example.test", "source": "/test/source", "id": "abc-1" }
            """;

        var result = await orchestrator.ValidateEvents(
            context,
            TestHelpers.CreateValidTopicSettings(),
            body
        );

        result.IsValid.ShouldBeTrue();
        result.DetectedSchema.ShouldBe(EventSchema.CloudEventV1_0);
        result.Events.ShouldNotBeNullAnd().Length.ShouldBe(1);
    }
}
