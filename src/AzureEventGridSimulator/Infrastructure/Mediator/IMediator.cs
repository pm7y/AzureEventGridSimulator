namespace AzureEventGridSimulator.Infrastructure.Mediator;

/// <summary>
///     Defines a mediator to dispatch requests to their corresponding handlers.
/// </summary>
public interface IMediator
{
    /// <summary>
    ///     Sends a request to a handler that does not return a value.
    /// </summary>
    /// <typeparam name="TRequest">
    ///     The type of request.
    /// </typeparam>
    /// <param name="request">
    ///     The request to send.
    /// </param>
    /// <param name="cancellationToken">
    ///     Optional cancellation token.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest;

    /// <summary>
    ///     Sends a request to a handler that returns a value.
    /// </summary>
    /// <typeparam name="TResponse">
    ///     The type of response.
    /// </typeparam>
    /// <param name="request">
    ///     The request to send.
    /// </param>
    /// <param name="cancellationToken">
    ///     Optional cancellation token.
    /// </param>
    /// <returns>
    ///     The response from the handler.
    /// </returns>
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    );
}
