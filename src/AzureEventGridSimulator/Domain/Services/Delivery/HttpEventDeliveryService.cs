using System.Net.Http.Headers;
using System.Text;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
/// Delivers events to HTTP webhook subscribers.
/// </summary>
public class HttpEventDeliveryService(
    IHttpClientFactory httpClientFactory,
    EventSchemaFormatterFactory formatterFactory,
    ILogger<HttpEventDeliveryService> logger
) : IEventDeliveryService
{
    /// <inheritdoc />
    public async Task<DeliveryResult> DeliverAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    )
    {
        if (delivery.Subscriber is not HttpSubscriberSettings httpSubscriber)
        {
            return new DeliveryResult(
                false,
                DeliveryOutcome.NetworkError,
                ErrorMessage: "Invalid subscriber type for HTTP delivery"
            );
        }

        try
        {
            // Determine the delivery schema
            var deliverySchema =
                httpSubscriber.DeliverySchema
                ?? delivery.Topic.OutputSchema
                ?? delivery.InputSchema;

            var formatter = formatterFactory.GetFormatter(deliverySchema);
            var json = formatter.Serialize(delivery.Event);

            using var content = new StringContent(json, Encoding.UTF8);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(formatter.ContentType);

            var httpClient = httpClientFactory.CreateClient("AzureEventGridSimulator");

            // Add standard Event Grid headers
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegEventTypeHeader,
                Constants.NotificationEventType
            );
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegSubscriptionNameHeader,
                httpSubscriber.Name.ToUpperInvariant()
            );
            httpClient.DefaultRequestHeaders.Add(
                Constants.AegDeliveryCountHeader,
                (delivery.AttemptCount + 1).ToString()
            );

            // Add schema-specific headers
            if (deliverySchema == EventSchema.EventGridSchema)
            {
                httpClient.DefaultRequestHeaders.Add(
                    Constants.AegDataVersionHeader,
                    delivery.Event.DataVersion ?? ""
                );
                httpClient.DefaultRequestHeaders.Add(Constants.AegMetadataVersionHeader, "1");
            }

            // Add any additional headers from the formatter
            foreach (var header in formatter.GetHeaders(delivery.Event))
            {
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            httpClient.Timeout = TimeSpan.FromSeconds(60);

            logger.LogDebug(
                "Attempting delivery of event {EventId} to {Endpoint} (attempt {Attempt})",
                delivery.Event.Id,
                httpSubscriber.Endpoint,
                delivery.AttemptCount + 1
            );

            var response = await httpClient.PostAsync(
                httpSubscriber.Endpoint,
                content,
                cancellationToken
            );

            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                logger.LogDebug(
                    "Event {EventId} delivered successfully to {Endpoint} with status {StatusCode}",
                    delivery.Event.Id,
                    httpSubscriber.Endpoint,
                    statusCode
                );

                return new DeliveryResult(true, DeliveryOutcome.Success, statusCode);
            }

            var reasonPhrase = response.ReasonPhrase ?? "Unknown error";

            logger.LogWarning(
                "Event {EventId} delivery failed to {Endpoint} with status {StatusCode}: {Reason}",
                delivery.Event.Id,
                httpSubscriber.Endpoint,
                statusCode,
                reasonPhrase
            );

            return new DeliveryResult(false, DeliveryOutcome.HttpError, statusCode, reasonPhrase);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            // Timeout (not user cancellation)
            logger.LogWarning(
                "Event {EventId} delivery timed out to {Endpoint}",
                delivery.Event.Id,
                httpSubscriber.Endpoint
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.Timeout,
                ErrorMessage: "Request timed out"
            );
        }
        catch (TaskCanceledException)
        {
            // User cancellation
            return new DeliveryResult(
                false,
                DeliveryOutcome.Cancelled,
                ErrorMessage: "Delivery cancelled"
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "Event {EventId} delivery failed to {Endpoint} with network error",
                delivery.Event.Id,
                httpSubscriber.Endpoint
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.NetworkError,
                ErrorMessage: ex.Message
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error delivering event {EventId} to {Endpoint}",
                delivery.Event.Id,
                httpSubscriber.Endpoint
            );

            return new DeliveryResult(
                false,
                DeliveryOutcome.NetworkError,
                ErrorMessage: ex.Message
            );
        }
    }
}
