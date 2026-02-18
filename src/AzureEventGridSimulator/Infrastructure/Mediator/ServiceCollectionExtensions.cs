using System.Reflection;

namespace AzureEventGridSimulator.Infrastructure.Mediator;

/// <summary>
///     Extension methods for registering the mediator and handlers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the mediator and registers all request handlers from the specified assembly.
    /// </summary>
    /// <param name="services">
    ///     The service collection.
    /// </param>
    /// <param name="assembly">
    ///     The assembly to scan for handlers.
    /// </param>
    /// <returns>
    ///     The service collection for chaining.
    /// </returns>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Assembly assembly
    )
    {
        services.AddSingleton<IMediator, Mediator>();

        // Find all handler types in the assembly
        var handlerTypes = assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t =>
                t.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && (
                            i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                            || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                        )
                    )
            );

        foreach (var handlerType in handlerTypes)
        {
            // Register each handler interface it implements
            var handlerInterfaces = handlerType
                .GetInterfaces()
                .Where(i =>
                    i.IsGenericType
                    && (
                        i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)
                        || i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                    )
                );

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddSingleton(handlerInterface, handlerType);
            }
        }

        return services;
    }
}
