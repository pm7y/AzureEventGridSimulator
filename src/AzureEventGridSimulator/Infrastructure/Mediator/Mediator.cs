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
        var handler = serviceProvider.GetService<IRequestHandler<TRequest>>();

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type {typeof(TRequest).Name}"
            );
        }

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
        var handler = serviceProvider.GetService(handlerType);

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type {requestType.Name}"
            );
        }

        var handleMethod = handlerType.GetMethod("Handle");
        return (Task<TResponse>)handleMethod!.Invoke(handler, [request, cancellationToken])!;
    }
}
