namespace AzureEventGridSimulator.Infrastructure.Mediator;

/// <summary>
/// Marker interface for a request (command/query) that does not return a value.
/// </summary>
public interface IRequest { }

/// <summary>
/// Marker interface for a request (command/query) that returns a value of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequest<out TResponse> { }
