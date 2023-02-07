namespace AzureEventGridSimulator.Infrastructure.Options;

using AzureEventGridSimulator.Domain.Converters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

internal sealed class ConfigureJsonOptions : IConfigureOptions<JsonOptions>
{
    private readonly EventGridEventConverter _eventGridEventConverter;
    private readonly CloudEventConverter _cloudEventConverter;

    public ConfigureJsonOptions(
        EventGridEventConverter eventGridEventConverter,
        CloudEventConverter cloudEventConverter)
    {
        _eventGridEventConverter = eventGridEventConverter;
        _cloudEventConverter = cloudEventConverter;
    }

    public void Configure(JsonOptions options)
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Converters.Add(_eventGridEventConverter);
        options.JsonSerializerOptions.Converters.Add(_cloudEventConverter);
    }
}
