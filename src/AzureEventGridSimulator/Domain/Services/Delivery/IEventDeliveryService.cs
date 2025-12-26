using AzureEventGridSimulator.Domain.Entities;

namespace AzureEventGridSimulator.Domain.Services.Delivery;

/// <summary>
///     Common interface for all event delivery services.
/// </summary>
public interface IEventDeliveryService
{
    /// <summary>
    ///     Delivers an event to a subscriber.
    /// </summary>
    /// <param name="delivery">
    ///     The pending delivery containing event and subscriber info.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation token.
    /// </param>
    /// <returns>
    ///     The result of the delivery attempt.
    /// </returns>
    Task<DeliveryResult> DeliverAsync(
        PendingDelivery delivery,
        CancellationToken cancellationToken
    );
}

/// <summary>
///     Result of a delivery attempt.
/// </summary>
/// <param name="Success">
///     Whether the delivery was successful.
/// </param>
/// <param name="Outcome">
///     The outcome classification.
/// </param>
/// <param name="HttpStatusCode">
///     HTTP status code, if applicable.
/// </param>
/// <param name="ErrorMessage">
///     Error message, if applicable.
/// </param>
public record DeliveryResult(
    bool Success,
    DeliveryOutcome Outcome,
    int? HttpStatusCode = null,
    string? ErrorMessage = null
);
