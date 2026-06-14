using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities.Management;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Management;

/// <summary>
///     The ARM scope of an event subscription, as taken from the request URL.
/// </summary>
public readonly record struct EventSubscriptionScope(
    string SubscriptionId,
    string ResourceGroupName,
    string TopicName
);

/// <summary>
///     Translates between the ARM <see cref="ArmEventSubscriptionResource" /> wire shape used by the
///     Azure.ResourceManager.EventGrid client and the simulator's internal
///     <see cref="HttpSubscriberSettings" />. Only WebHook destinations are supported.
/// </summary>
public static class EventSubscriptionMapper
{
    public const string WebHookEndpointType = "WebHook";

    private const string EventSubscriptionType = "Microsoft.EventGrid/topics/eventSubscriptions";

    /// <summary>
    ///     Maps an ARM event subscription resource to an HTTP subscriber. Returns false when the
    ///     destination is missing or is not a WebHook (the only destination type the management API
    ///     supports today).
    /// </summary>
    public static bool TryMapToHttpSubscriber(
        string name,
        ArmEventSubscriptionResource resource,
        out HttpSubscriberSettings? subscriber
    )
    {
        subscriber = null;

        var destination = resource.Properties?.Destination;
        if (
            destination is null
            || !string.Equals(
                destination.EndpointType,
                WebHookEndpointType,
                StringComparison.OrdinalIgnoreCase
            )
            || string.IsNullOrWhiteSpace(destination.Properties?.EndpointUrl)
        )
        {
            return false;
        }

        subscriber = new HttpSubscriberSettings
        {
            Name = name,
            Endpoint = destination.Properties.EndpointUrl,
            Filter = MapFilter(resource.Properties?.Filter),
        };

        return true;
    }

    public static ArmEventSubscriptionResource MapToArm(
        EventSubscriptionScope scope,
        HttpSubscriberSettings subscriber
    ) =>
        new()
        {
            Id = BuildResourceId(scope, subscriber.Name),
            Name = subscriber.Name,
            Type = EventSubscriptionType,
            Properties = new ArmEventSubscriptionProperties
            {
                ProvisioningState = "Succeeded",
                Destination = new ArmEventSubscriptionDestination
                {
                    EndpointType = WebHookEndpointType,
                    Properties = new ArmWebHookDestinationProperties
                    {
                        EndpointUrl = subscriber.Endpoint,
                        EndpointBaseUrl = subscriber.Endpoint,
                    },
                },
                Filter = MapFilter(subscriber.Filter),
            },
        };

    public static string BuildResourceId(EventSubscriptionScope scope, string name) =>
        $"/subscriptions/{scope.SubscriptionId}/resourceGroups/{scope.ResourceGroupName}"
        + $"/providers/Microsoft.EventGrid/topics/{scope.TopicName}/eventSubscriptions/{name}";

    private static FilterSetting? MapFilter(ArmEventSubscriptionFilter? filter)
    {
        if (filter is null)
        {
            return null;
        }

        return new FilterSetting
        {
            IncludedEventTypes = filter.IncludedEventTypes is { Count: > 0 }
                ? filter.IncludedEventTypes.ToList()
                : null,
            SubjectBeginsWith = filter.SubjectBeginsWith,
            SubjectEndsWith = filter.SubjectEndsWith,
            IsSubjectCaseSensitive = filter.IsSubjectCaseSensitive ?? false,
            AdvancedFilters = filter
                .AdvancedFilters?.Select(MapAdvancedFilter)
                .ToList<AdvancedFilterSetting>(),
        };
    }

    private static ArmEventSubscriptionFilter? MapFilter(FilterSetting? filter)
    {
        if (filter is null)
        {
            return null;
        }

        return new ArmEventSubscriptionFilter
        {
            IncludedEventTypes = filter.IncludedEventTypes?.ToList(),
            SubjectBeginsWith = filter.SubjectBeginsWith,
            SubjectEndsWith = filter.SubjectEndsWith,
            IsSubjectCaseSensitive = filter.IsSubjectCaseSensitive,
            AdvancedFilters = filter.AdvancedFilters?.Select(MapAdvancedFilter).ToList(),
        };
    }

    private static AdvancedFilterSetting MapAdvancedFilter(ArmAdvancedFilter filter) =>
        new()
        {
            OperatorType = Enum.Parse<AdvancedFilterSetting.AdvancedFilterOperatorType>(
                filter.OperatorType ?? string.Empty,
                ignoreCase: true
            ),
            Key = filter.Key,
            Value = filter.Value.HasValue ? ToClrValue(filter.Value.Value) : null,
            Values = filter.Values?.Select(ToClrValue).ToList(),
        };

    private static ArmAdvancedFilter MapAdvancedFilter(AdvancedFilterSetting filter) =>
        new()
        {
            OperatorType = filter.OperatorType.ToString(),
            Key = filter.Key,
            Value = filter.Value is null ? null : JsonSerializer.SerializeToElement(filter.Value),
            Values = filter.Values?.Select(v => JsonSerializer.SerializeToElement(v)).ToList(),
        };

    private static object ToClrValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.GetRawText(),
        };
}
