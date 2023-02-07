namespace AzureEventGridSimulator.Infrastructure.Routing;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureEventGridSimulator.Infrastructure.Filters;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Logging;

public sealed class TopicMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    private readonly ILogger _logger;

    public TopicMatcherPolicy(ILogger<TopicMatcherPolicy> logger)
    {
        _logger = logger;
    }

    public override int Order { get; } = int.MinValue;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        if (endpoints == null)
        {
            throw new ArgumentNullException(nameof(endpoints));
        }

        for (var i = 0; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            var eventTypeAttribute = endpoint.Metadata.GetMetadata<EventTypeAttribute>();

            if (eventTypeAttribute != null)
            {
                return true;
            }
        }

        return false;
    }

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        var topic = httpContext.Features.Get<TopicSettings>();
        if (topic == null)
        {
            _logger.LogDebug("Topic '{TopicName}' detected. Permitting {EventType} routes only.", topic.Name, topic.Type);
        }
        else
        {
            _logger.LogDebug("No topic was detected. Disabling all event routes.");
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            // disable any endpoint that specifies an event type AND is not for the topic's event type

            var attrib = candidates[i].Endpoint.Metadata.GetMetadata<EventTypeAttribute>();
            if (attrib != null && (topic == null || attrib.EventType != topic.Type))
            {
                candidates.SetValidity(i, false);
            }
        }

        return Task.CompletedTask;
    }
}
