namespace AzureEventGridSimulator.Infrastructure.ModelBinders;

using Microsoft.AspNetCore.Mvc.ModelBinding;

internal static class CustomBindingSources
{
    public static BindingSource HttpContextFeatures { get; } = new BindingSource(nameof(HttpContextFeatures), "HttpContext Features", true, true);
}
