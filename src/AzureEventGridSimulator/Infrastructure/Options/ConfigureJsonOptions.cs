namespace AzureEventGridSimulator.Infrastructure.Options;

using AzureEventGridSimulator.Domain.Converters;
using AzureEventGridSimulator.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

internal sealed class ConfigureJsonOptions : IConfigureOptions<JsonOptions>
{
    private readonly EventConverter<EventGridEvent> _eventGridEventConverter;
    private readonly EventConverter<CloudEvent> _cloudEventConverter;

    public ConfigureJsonOptions(
        EventConverter<EventGridEvent> eventGridEventConverter,
        EventConverter<CloudEvent> cloudEventConverter)
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
