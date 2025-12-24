namespace AzureEventGridSimulator.Infrastructure.Mediator;

/// <summary>
/// Default implementation of <see cref="IMediator" /> that resolves handlers from the service
/// provider.
/// </summary>
public class Mediator(IServiceProvider serviceProvider) : IMediator
{
    /// <inheritdoc />
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest>>();
        return handler.Handle(request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    )
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(
            requestType,
            typeof(TResponse)
        );
        var handler = serviceProvider.GetRequiredService(handlerType);
        var handleMethod =
            handlerType.GetMethod("Handle")
            ?? throw new InvalidOperationException(
                $"Handle method not found on handler type {handlerType.Name}"
            );

        var result =
            handleMethod.Invoke(handler, [request, cancellationToken])
            ?? throw new InvalidOperationException($"Handler for {requestType.Name} returned null");

        return (Task<TResponse>)result;
    }
}
