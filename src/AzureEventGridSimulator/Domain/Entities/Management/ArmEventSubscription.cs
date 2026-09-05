using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Domain.Entities.Management;

/// <summary>
///     ARM resource representation of an Event Grid event subscription, matching the JSON shape the
///     Azure.ResourceManager.EventGrid client sends and expects on the control plane.
/// </summary>
public class ArmEventSubscriptionResource
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("properties")]
    public ArmEventSubscriptionProperties? Properties { get; set; }
}

public class ArmEventSubscriptionProperties
{
    [JsonPropertyName("provisioningState")]
    public string? ProvisioningState { get; set; }

    [JsonPropertyName("destination")]
    public ArmEventSubscriptionDestination? Destination { get; set; }

    [JsonPropertyName("filter")]
    public ArmEventSubscriptionFilter? Filter { get; set; }
}

public class ArmEventSubscriptionDestination
{
    [JsonPropertyName("endpointType")]
    public string? EndpointType { get; set; }

    [JsonPropertyName("properties")]
    public ArmEventSubscriptionDestinationProperties? Properties { get; set; }
}

public class ArmEventSubscriptionDestinationProperties
{
    /// <summary>
    ///     The full webhook URL. Azure treats this as write-only and only returns
    ///     <see cref="EndpointBaseUrl" /> on reads; the simulator echoes it back so that the
    ///     read-modify-write pattern (e.g. updating a filter) preserves the destination.
    /// </summary>
    [JsonPropertyName("endpointUrl")]
    public string? EndpointUrl { get; set; }

    [JsonPropertyName("endpointBaseUrl")]
    public string? EndpointBaseUrl { get; set; }

    // Storage-queue destination properties (ARM nests destination-type-specific properties under the
    // one "properties" object; only the fields for the active endpointType are populated).
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("queueName")]
    public string? QueueName { get; set; }
}

public class ArmEventSubscriptionFilter
{
    [JsonPropertyName("includedEventTypes")]
    public List<string>? IncludedEventTypes { get; set; }

    [JsonPropertyName("subjectBeginsWith")]
    public string? SubjectBeginsWith { get; set; }

    [JsonPropertyName("subjectEndsWith")]
    public string? SubjectEndsWith { get; set; }

    [JsonPropertyName("isSubjectCaseSensitive")]
    public bool? IsSubjectCaseSensitive { get; set; }

    [JsonPropertyName("advancedFilters")]
    public List<ArmAdvancedFilter>? AdvancedFilters { get; set; }
}

public class ArmAdvancedFilter
{
    [JsonPropertyName("operatorType")]
    public string? OperatorType { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("values")]
    public List<JsonElement>? Values { get; set; }
}

/// <summary>
///     ARM list response wrapper for a collection of event subscriptions.
/// </summary>
public class ArmEventSubscriptionList
{
    [JsonPropertyName("value")]
    public List<ArmEventSubscriptionResource> Value { get; set; } = [];

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}
