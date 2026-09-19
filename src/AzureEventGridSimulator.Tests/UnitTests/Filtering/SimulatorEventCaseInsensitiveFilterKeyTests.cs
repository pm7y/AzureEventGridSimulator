using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Domain.Filtering;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Filtering;

/// <summary>
///     Azure resolves top-level filter keys case-insensitively. These tests exercise
///     AcceptsEvent(SimulatorEvent), which is the overload used when delivering events
///     to subscribers, for both the Event Grid and CloudEvents schemas.
/// </summary>
[Trait("Category", "unit")]
public class SimulatorEventCaseInsensitiveFilterKeyTests
{
    private const string EventId = "test-id-123";
    private const string EventSubject = "/test/subject";
    private const string EventType = "Test.EventType";
    private const string EventSource = "/test/source";
    private const string EventDataVersion = "1.0";

    private static SimulatorEvent CreateSimulatorEvent(EventSchema schema)
    {
        if (schema == EventSchema.CloudEventV1_0)
        {
            var cloudEvent = TestHelpers.CreateValidCloudEvent(
                type: EventType,
                source: EventSource,
                id: EventId,
                subject: EventSubject,
                data: new { Name = "StringValue" }
            );
            cloudEvent.DataSchema = EventDataVersion;
            return SimulatorEvent.FromCloudEvent(cloudEvent);
        }

        var gridEvent = TestHelpers.CreateValidEventGridEvent(
            id: EventId,
            subject: EventSubject,
            eventType: EventType,
            dataVersion: EventDataVersion,
            data: new { Name = "StringValue" }
        );
        gridEvent.SetTopic(EventSource);
        return SimulatorEvent.FromEventGridEvent(gridEvent);
    }

    private static bool Evaluate(
        AdvancedFilterSetting filter,
        EventSchema schema = EventSchema.EventGridSchema
    )
    {
        var filterConfig = new FilterSetting { AdvancedFilters = [filter] };

        return filterConfig.AcceptsEvent(CreateSimulatorEvent(schema));
    }

    [Theory]
    [InlineData(EventSchema.EventGridSchema, "eventType", EventType)]
    [InlineData(EventSchema.EventGridSchema, "eventtype", EventType)]
    [InlineData(EventSchema.EventGridSchema, "EVENTTYPE", EventType)]
    [InlineData(EventSchema.CloudEventV1_0, "type", EventType)]
    [InlineData(EventSchema.CloudEventV1_0, "TYPE", EventType)]
    [InlineData(EventSchema.EventGridSchema, "id", EventId)]
    [InlineData(EventSchema.EventGridSchema, "ID", EventId)]
    [InlineData(EventSchema.CloudEventV1_0, "iD", EventId)]
    [InlineData(EventSchema.EventGridSchema, "Subject", EventSubject)]
    [InlineData(EventSchema.EventGridSchema, "SUBJECT", EventSubject)]
    [InlineData(EventSchema.CloudEventV1_0, "SUBJECT", EventSubject)]
    [InlineData(EventSchema.EventGridSchema, "topic", EventSource)]
    [InlineData(EventSchema.EventGridSchema, "TOPIC", EventSource)]
    [InlineData(EventSchema.CloudEventV1_0, "source", EventSource)]
    [InlineData(EventSchema.CloudEventV1_0, "SOURCE", EventSource)]
    [InlineData(EventSchema.EventGridSchema, "dataVersion", EventDataVersion)]
    [InlineData(EventSchema.EventGridSchema, "DATAVERSION", EventDataVersion)]
    [InlineData(EventSchema.CloudEventV1_0, "dataschema", EventDataVersion)]
    [InlineData(EventSchema.CloudEventV1_0, "dataSchema", EventDataVersion)]
    [InlineData(EventSchema.CloudEventV1_0, "DATASCHEMA", EventDataVersion)]
    public void GivenTopLevelKeyInAnyCase_WhenStringInMatchesEventValue_ThenEventAccepted(
        EventSchema schema,
        string key,
        string eventValue
    )
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = key,
                    Values = [eventValue],
                },
                schema
            )
            .ShouldBeTrue($"{schema} {key}");
    }

    [Theory]
    [InlineData(EventSchema.EventGridSchema, "eventType", EventType)]
    [InlineData(EventSchema.EventGridSchema, "eventtype", EventType)]
    [InlineData(EventSchema.EventGridSchema, "EVENTTYPE", EventType)]
    [InlineData(EventSchema.CloudEventV1_0, "TYPE", EventType)]
    [InlineData(EventSchema.EventGridSchema, "ID", EventId)]
    public void GivenTopLevelKeyInAnyCase_WhenStringNotInContainsEventValue_ThenEventRejected(
        EventSchema schema,
        string key,
        string eventValue
    )
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringNotIn,
                    Key = key,
                    Values = [eventValue],
                },
                schema
            )
            .ShouldBeFalse($"{schema} {key}");
    }

    [Theory]
    [InlineData("Subject")]
    [InlineData("SUBJECT")]
    [InlineData("sUBJECT")]
    public void GivenSubjectKeyInAnyCase_WhenStringBeginsWithMatches_ThenEventAccepted(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .StringBeginsWith,
                    Key = key,
                    Values = ["/test/"],
                }
            )
            .ShouldBeTrue(key);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("SUBJECT")]
    [InlineData("EVENTTYPE")]
    [InlineData("DATA")]
    public void GivenPresentKeyInAnyCase_WhenIsNullOrUndefined_ThenEventRejected(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting
                        .AdvancedFilterOperatorType
                        .IsNullOrUndefined,
                    Key = key,
                }
            )
            .ShouldBeFalse(key);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("SUBJECT")]
    [InlineData("EVENTTYPE")]
    [InlineData("DATA")]
    public void GivenPresentKeyInAnyCase_WhenIsNotNull_ThenEventAccepted(string key)
    {
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.IsNotNull,
                    Key = key,
                }
            )
            .ShouldBeTrue(key);
    }

    [Fact]
    public void GivenKeyCaseDiffersButValueDoesNotMatch_WhenStringIn_ThenEventRejected()
    {
        // Case-insensitive key resolution must not loosen the value comparison
        Evaluate(
                new AdvancedFilterSetting
                {
                    OperatorType = AdvancedFilterSetting.AdvancedFilterOperatorType.StringIn,
                    Key = "EVENTTYPE",
                    Values = ["Some.Other.EventType"],
                }
            )
            .ShouldBeFalse();
    }
}
