using System.Text.Json;

namespace AzureEventGridSimulator.Infrastructure.JsonConverters;

/// <summary>
///     Provides shared JsonSerializerOptions instances configured to match Azure Event Grid's lenient behavior.
/// </summary>
public static class JsonSerializerOptionsProvider
{
    /// <summary>
    ///     Gets the default options for parsing Event Grid events.
    ///     Azure is lenient and allows trailing commas in JSON.
    /// </summary>
    public static JsonSerializerOptions Default { get; } =
        new() { AllowTrailingCommas = true, PropertyNameCaseInsensitive = true };
}
