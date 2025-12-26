namespace AzureEventGridSimulator.Infrastructure.Mediator;

/// <summary>
///     Handles a request that does not return a value.
/// </summary>
/// <typeparam name="TRequest">
///     The type of request being handled.
/// </typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest
{
    /// <summary>
    ///     Handles the request.
    /// </summary>
    /// <param name="request">
    ///     The request to handle.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation token.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
///     Handles a request that returns a value.
/// </summary>
/// <typeparam name="TRequest">
///     The type of request being handled.
/// </typeparam>
/// <typeparam name="TResponse">
///     The type of response.
/// </typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    ///     Handles the request.
    /// </summary>
    /// <param name="request">
    ///     The request to handle.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation token.
    /// </param>
    /// <returns>
    ///     The response.
    /// </returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
