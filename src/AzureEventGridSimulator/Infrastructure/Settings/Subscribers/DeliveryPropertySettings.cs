using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

/// <summary>
/// Settings for a delivery property that can be added to Service Bus messages.
/// </summary>
public class DeliveryPropertySettings
{
    /// <summary>
    /// Gets or sets the type of property: "static" or "dynamic".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the value of the property.
    /// For static properties, this is the literal value.
    /// For dynamic properties, this is the path to the event property (e.g., "Subject",
    /// "data.customerId").
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; }

    /// <summary>
    /// Gets whether this is a static property.
    /// </summary>
    [JsonIgnore]
    public bool IsStatic => string.Equals(Type, "static", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether this is a dynamic property.
    /// </summary>
    [JsonIgnore]
    public bool IsDynamic => string.Equals(Type, "dynamic", StringComparison.OrdinalIgnoreCase);

    public void Validate(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new ArgumentException(
                $"Property '{propertyName}' must have a type.",
                nameof(Type)
            );
        }

        if (!IsStatic && !IsDynamic)
        {
            throw new ArgumentException(
                $"Property '{propertyName}' type must be 'static' or 'dynamic', got '{Type}'.",
                nameof(Type)
            );
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            throw new ArgumentException(
                $"Property '{propertyName}' must have a value.",
                nameof(Value)
            );
        }
    }
}
