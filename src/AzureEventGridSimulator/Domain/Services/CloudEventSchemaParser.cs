using System;
using System.Linq;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Domain.Services;

/// <summary>
/// Parses events using the CloudEvents v1.0 schema.
/// Supports both binary and structured content modes.
/// </summary>
public class CloudEventSchemaParser(EventSchemaDetector schemaDetector) : IEventSchemaParser
{
    /// <inheritdoc />
    public EventSchema Schema => EventSchema.CloudEventV1_0;

    /// <inheritdoc />
    public SimulatorEvent[] Parse(HttpContext context, string requestBody)
    {
        if (schemaDetector.IsBinaryMode(context))
        {
            return ParseBinaryMode(context, requestBody);
        }

        if (schemaDetector.IsBatchMode(context))
        {
            return ParseBatchStructuredMode(requestBody);
        }

        return ParseStructuredMode(requestBody);
    }

    /// <summary>
    /// Parses a CloudEvent from binary content mode.
    /// In binary mode, CloudEvents attributes are in HTTP headers.
    /// </summary>
    private SimulatorEvent[] ParseBinaryMode(HttpContext context, string requestBody)
    {
        var headers = context.Request.Headers;

        var cloudEvent = new CloudEvent
        {
            SpecVersion = GetHeaderValue(headers, Constants.CeSpecVersionHeader),
            Id = GetHeaderValue(headers, Constants.CeIdHeader),
            Source = GetHeaderValue(headers, Constants.CeSourceHeader),
            Type = GetHeaderValue(headers, Constants.CeTypeHeader),
            Time = GetHeaderValue(headers, Constants.CeTimeHeader),
            Subject = GetHeaderValue(headers, Constants.CeSubjectHeader),
            DataContentType =
                GetHeaderValue(headers, Constants.CeDataContentTypeHeader)
                ?? context.Request.ContentType,
            DataSchema = GetHeaderValue(headers, Constants.CeDataSchemaHeader),
        };

        // Parse the body as data
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            // Try to parse as JSON, otherwise treat as string
            try
            {
                using var doc = JsonDocument.Parse(requestBody);
                cloudEvent.Data = JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText());
            }
            catch (JsonException)
            {
                cloudEvent.Data = requestBody;
            }
        }

        return new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
    }

    /// <summary>
    /// Parses a single CloudEvent from structured content mode.
    /// In structured mode, all CloudEvents attributes are in the JSON body.
    /// </summary>
    private SimulatorEvent[] ParseStructuredMode(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("Request body is empty.");
        }

        CloudEvent cloudEvent;

        try
        {
            using var document = JsonDocument.Parse(requestBody);

            // Check if it's an array (single event in array format)
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                if (document.RootElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("No events found in the request body.");
                }

                // Handle single event in array format
                var events = JsonSerializer.Deserialize<CloudEvent[]>(requestBody);
                return events.Select(SimulatorEvent.FromCloudEvent).ToArray();
            }

            cloudEvent = JsonSerializer.Deserialize<CloudEvent>(requestBody);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse CloudEvent: {ex.Message}", ex);
        }

        if (cloudEvent == null)
        {
            throw new InvalidOperationException("Failed to parse CloudEvent from request body.");
        }

        return new[] { SimulatorEvent.FromCloudEvent(cloudEvent) };
    }

    /// <summary>
    /// Parses multiple CloudEvents from batch structured content mode.
    /// </summary>
    private SimulatorEvent[] ParseBatchStructuredMode(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            throw new InvalidOperationException("Request body is empty.");
        }

        CloudEvent[] events;

        try
        {
            events = JsonSerializer.Deserialize<CloudEvent[]>(requestBody);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse CloudEvents batch: {ex.Message}",
                ex
            );
        }

        if (events == null || events.Length == 0)
        {
            throw new InvalidOperationException("No events found in the request body.");
        }

        return events.Select(SimulatorEvent.FromCloudEvent).ToArray();
    }

    /// <inheritdoc />
    public void Validate(SimulatorEvent[] events)
    {
        foreach (var evt in events)
        {
            evt.Validate();
        }
    }

    /// <summary>
    /// Gets and decodes a header value, handling percent-encoding per CloudEvents HTTP binding spec.
    /// </summary>
    private static string GetHeaderValue(IHeaderDictionary headers, string headerName)
    {
        if (!headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // CloudEvents HTTP Protocol Binding requires percent-encoding for certain characters
        // in header values (spaces, non-ASCII, etc.). We need to decode them.
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            // If decoding fails, return the original value
            return value;
        }
    }
}
