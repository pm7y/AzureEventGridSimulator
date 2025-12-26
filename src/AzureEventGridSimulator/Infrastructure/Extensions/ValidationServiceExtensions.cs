using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Domain.Services.Validation;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

/// <summary>
///     Extension methods for registering event validation services.
/// </summary>
public static class ValidationServiceExtensions
{
    /// <summary>
    ///     Registers event validation services (routing and validation pipeline).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEventGridValidation(this IServiceCollection services)
    {
        // All validation services are stateless and can be singletons
        services.AddSingleton<RequestRouter>();
        services.AddSingleton<RequestBodyValidator>();
        services.AddSingleton<ContentTypeValidator>();
        services.AddSingleton<EventValidationOrchestrator>();

        return services;
    }
}
