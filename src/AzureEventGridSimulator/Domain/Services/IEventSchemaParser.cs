using AzureEventGridSimulator.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Interface for parsing events from HTTP requests based on their schema.
/// </summary>
public interface IEventSchemaParser
{
    /// <summary>
    /// Gets the schema type this parser handles.
    /// </summary>
    EventSchema Schema { get; }

    /// <summary>
    /// Parses events from the HTTP request context and body.
    /// </summary>
    /// <param name="context">The HTTP context containing headers.</param>
    /// <param name="requestBody">The raw request body.</param>
    /// <returns>An array of parsed SimulatorEvents.</returns>
    SimulatorEvent[] Parse(HttpContext context, string requestBody);

    /// <summary>
    /// Validates the parsed events according to the schema rules.
    /// </summary>
    /// <param name="events">The events to validate.</param>
    void Validate(SimulatorEvent[] events);
}
