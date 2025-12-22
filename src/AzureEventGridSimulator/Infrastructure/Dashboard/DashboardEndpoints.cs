#nullable enable

using AzureEventGridSimulator.Domain.Entities.Dashboard;
using AzureEventGridSimulator.Domain.Services.Dashboard;

namespace AzureEventGridSimulator.Infrastructure.Dashboard;

/// <summary>
/// Dashboard API endpoints for retrieving event history and statistics.
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>
    /// Maps all dashboard API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dashboard/api");

        group.MapGet("/events", GetEvents);
        group.MapGet("/events/{id}", GetEventById);
        group.MapGet("/stats", GetStats);
        group.MapGet("/rejections", GetRejections);
        group.MapDelete("/clear", ClearHistory);

        return endpoints;
    }

    /// <summary>
    /// GET /dashboard/api/events - Returns list of recent events.
    /// </summary>
    private static IResult GetEvents(IEventHistoryService eventHistoryService, string? topic = null)
    {
        var events = eventHistoryService.GetRecentEvents(topic);
        var response = events.Select(MapToEventSummary).ToList();
        return Results.Ok(response);
    }

    /// <summary>
    /// GET /dashboard/api/events/{id} - Returns details of a specific event.
    /// </summary>
    private static IResult GetEventById(string id, IEventHistoryService eventHistoryService)
    {
        var evt = eventHistoryService.GetEvent(id);
        if (evt == null)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapToEventDetails(evt));
    }

    /// <summary>
    /// GET /dashboard/api/stats - Returns dashboard statistics.
    /// </summary>
    private static IResult GetStats(IEventHistoryService eventHistoryService)
    {
        var stats = eventHistoryService.GetStats();
        return Results.Ok(MapToStatsResponse(stats));
    }

    /// <summary>
    /// GET /dashboard/api/rejections - Returns list of rejected events.
    /// </summary>
    private static IResult GetRejections(IEventHistoryService eventHistoryService)
    {
        var rejections = eventHistoryService.GetRecentRejections();
        var response = rejections.Select(MapToRejectionResponse).ToList();
        return Results.Ok(response);
    }

    /// <summary>
    /// DELETE /dashboard/api/clear - Clears all event history and rejections.
    /// </summary>
    private static IResult ClearHistory(EventHistoryStore store)
    {
        store.Clear();
        return Results.Ok(new { message = "History cleared" });
    }

    // Response DTOs and Mapping

    private static EventSummaryResponse MapToEventSummary(EventHistoryRecord record)
    {
        return new EventSummaryResponse
        {
            Id = record.Id,
            EventType = record.EventType,
            Subject = record.Subject,
            TopicName = record.TopicName,
            ReceivedAt = record.ReceivedAt,
            Deliveries = record.GetDeliveries().Select(MapToDeliverySummary).ToList(),
        };
    }

    private static EventDetailsResponse MapToEventDetails(EventHistoryRecord record)
    {
        return new EventDetailsResponse
        {
            Id = record.Id,
            EventType = record.EventType,
            Subject = record.Subject,
            Source = record.Source,
            EventTime = record.EventTime,
            TopicName = record.TopicName,
            TopicPort = record.TopicPort,
            InputSchema = record.InputSchema.ToString(),
            ReceivedAt = record.ReceivedAt,
            PayloadJson = record.PayloadJson,
            Deliveries = record.GetDeliveries().Select(MapToDeliveryDetails).ToList(),
        };
    }

    private static DeliverySummaryResponse MapToDeliverySummary(DeliveryRecord delivery)
    {
        return new DeliverySummaryResponse
        {
            SubscriberName = delivery.SubscriberName,
            Status = delivery.Status.ToString(),
        };
    }

    private static DeliveryDetailsResponse MapToDeliveryDetails(DeliveryRecord delivery)
    {
        return new DeliveryDetailsResponse
        {
            SubscriberName = delivery.SubscriberName,
            SubscriberType = delivery.SubscriberType,
            Endpoint = delivery.Endpoint,
            Status = delivery.Status.ToString(),
            LastAttemptAt = delivery.LastAttemptAt,
            CompletedAt = delivery.CompletedAt,
            Attempts = delivery.Attempts.Select(MapToAttemptResponse).ToList(),
        };
    }

    private static AttemptResponse MapToAttemptResponse(AttemptRecord attempt)
    {
        return new AttemptResponse
        {
            AttemptNumber = attempt.AttemptNumber,
            AttemptTime = attempt.AttemptedAt,
            Outcome = attempt.Outcome.ToString(),
            StatusCode = attempt.HttpStatusCode,
            ErrorMessage = attempt.ErrorMessage,
        };
    }

    private static StatsResponse MapToStatsResponse(DashboardStats stats)
    {
        return new StatsResponse
        {
            TotalEventsReceived = stats.TotalEventsReceived,
            EventsInHistory = stats.EventsInHistory,
            TotalDelivered = stats.TotalDelivered,
            TotalFailed = stats.TotalFailed,
            TotalPending = stats.TotalPending,
            TotalRejected = stats.TotalRejected,
            TopicsActive = stats.TopicsActive,
            OldestEventTime = stats.OldestEventTime,
            NewestEventTime = stats.NewestEventTime,
        };
    }

    private static RejectionResponse MapToRejectionResponse(RejectedEventRecord rejection)
    {
        return new RejectionResponse
        {
            Id = rejection.Id,
            RejectedAt = rejection.RejectedAt,
            TopicName = rejection.TopicName,
            TopicPort = rejection.TopicPort,
            StatusCode = (int)rejection.StatusCode,
            ErrorMessage = rejection.ErrorMessage,
            RawBody = rejection.RawBody,
            ContentType = rejection.ContentType,
        };
    }
}

// Response DTOs

internal sealed class EventSummaryResponse
{
    public required string Id { get; init; }
    public required string EventType { get; init; }
    public string? Subject { get; init; }
    public required string TopicName { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public List<DeliverySummaryResponse> Deliveries { get; init; } = [];
}

internal sealed class EventDetailsResponse
{
    public required string Id { get; init; }
    public required string EventType { get; init; }
    public string? Subject { get; init; }
    public string? Source { get; init; }
    public string? EventTime { get; init; }
    public required string TopicName { get; init; }
    public int TopicPort { get; init; }
    public required string InputSchema { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public string? PayloadJson { get; init; }
    public List<DeliveryDetailsResponse> Deliveries { get; init; } = [];
}

internal sealed class DeliverySummaryResponse
{
    public required string SubscriberName { get; init; }
    public required string Status { get; init; }
}

internal sealed class DeliveryDetailsResponse
{
    public required string SubscriberName { get; init; }
    public required string SubscriberType { get; init; }
    public required string Endpoint { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset? LastAttemptAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public List<AttemptResponse> Attempts { get; init; } = [];
}

internal sealed class AttemptResponse
{
    public int AttemptNumber { get; init; }
    public DateTimeOffset AttemptTime { get; init; }
    public required string Outcome { get; init; }
    public int? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
}

internal sealed class StatsResponse
{
    public int TotalEventsReceived { get; init; }
    public int EventsInHistory { get; init; }
    public int TotalDelivered { get; init; }
    public int TotalFailed { get; init; }
    public int TotalPending { get; init; }
    public int TotalRejected { get; init; }
    public int TopicsActive { get; init; }
    public DateTimeOffset? OldestEventTime { get; init; }
    public DateTimeOffset? NewestEventTime { get; init; }
}

internal sealed class RejectionResponse
{
    public required string Id { get; init; }
    public DateTimeOffset RejectedAt { get; init; }
    public required string TopicName { get; init; }
    public int TopicPort { get; init; }
    public int StatusCode { get; init; }
    public required string ErrorMessage { get; init; }
    public string? RawBody { get; init; }
    public string? ContentType { get; init; }
}
