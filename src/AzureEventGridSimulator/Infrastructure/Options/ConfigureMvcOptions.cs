namespace AzureEventGridSimulator.Infrastructure.Options;

using AzureEventGridSimulator.Infrastructure.ModelBinders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

internal sealed class ConfigureMvcOptions : IConfigureOptions<MvcOptions>
{
    private readonly HttpContextFeaturesModelBinderProvider _httpContextFeaturesModelBinderProvider;

    public ConfigureMvcOptions(HttpContextFeaturesModelBinderProvider httpContextFeaturesModelBinderProvider)
    {
        _httpContextFeaturesModelBinderProvider = httpContextFeaturesModelBinderProvider;
    }

    public void Configure(MvcOptions options)
    {
        options.EnableEndpointRouting = false;
        options.ModelBinderProviders.Insert(0, _httpContextFeaturesModelBinderProvider);
    }
}
