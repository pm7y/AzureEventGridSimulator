using System;
using Azure.Messaging;
using AzureEventGridSimulator.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AzureEventGridSimulator.Domain.Services;

public static class CloudEventValidateService
{
    public static void Validate(CloudEventGridEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.Id))
        {
            throw new InvalidOperationException($"Required property '{nameof(@event.Id)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(@event.Subject))
        {
            throw new InvalidOperationException($"Required property '{nameof(@event.Subject)}' was not set.");
        }

        if (string.IsNullOrWhiteSpace(@event.Type))
        {
            throw new InvalidOperationException($"Required property '{nameof(@event.Type)}' was not set.");
        }
            //}

            //if (string.IsNullOrWhiteSpace(EventTime))
            //{
            //    throw new InvalidOperationException($"Required property '{nameof(EventTime)}' was not set.");
            //}

            //if (!EventTimeIsValid)
            //{
            //    throw new InvalidOperationException($"The event time property '{nameof(EventTime)}' was not a valid date/time.");
            //}

            //if (EventTimeParsed.Kind == DateTimeKind.Unspecified)
            //{
            //    throw new InvalidOperationException($"Property '{nameof(EventTime)}' must be either Local or UTC.");
            //}

            //if (MetadataVersion != null && MetadataVersion != "1")
            //{
            //    throw new
            //        InvalidOperationException($"Property '{nameof(MetadataVersion)}' was found to be set to '{MetadataVersion}', but was expected to either be null or be set to 1.");
            //}

            //if (!string.IsNullOrEmpty(Topic))
            //{
            //    throw new InvalidOperationException($"Property '{nameof(Topic)}' was found to be set to '{Topic}', but was expected to either be null/empty.");
            //}

    }

}
