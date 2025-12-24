using System.Text.Json.Serialization;

namespace AzureEventGridSimulator.Infrastructure.Settings;

public class FilterSetting
{
    [JsonPropertyName("includedEventTypes")]
    public ICollection<string>? IncludedEventTypes { get; init; }

    [JsonPropertyName("isSubjectCaseSensitive")]
    public bool IsSubjectCaseSensitive { get; init; }

    [JsonPropertyName("subjectBeginsWith")]
    public string? SubjectBeginsWith { get; init; }

    [JsonPropertyName("subjectEndsWith")]
    public string? SubjectEndsWith { get; init; }

    [JsonPropertyName("advancedFilters")]
    public ICollection<AdvancedFilterSetting>? AdvancedFilters { get; init; }

    internal void Validate()
    {
        // Azure Event Grid allows up to 25 advanced filters per subscription
        if (AdvancedFilters?.Count > 25)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AdvancedFilters),
                "Advanced filtering is limited to 25 advanced filters per event grid subscription."
            );
        }

        foreach (var advancedFilter in AdvancedFilters ?? [])
        {
            advancedFilter.Validate();
        }
    }
}
