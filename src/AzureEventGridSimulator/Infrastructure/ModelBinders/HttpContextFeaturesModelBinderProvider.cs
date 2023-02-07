namespace AzureEventGridSimulator.Infrastructure.ModelBinders;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

internal sealed class HttpContextFeaturesModelBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context.BindingInfo.BindingSource == CustomBindingSources.HttpContextFeatures)
        {
            return new HttpContextFeaturesModelBinder();
        }
        return null;
    }

    private class HttpContextFeaturesModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var item = bindingContext.HttpContext.Features[bindingContext.ModelType];

            bindingContext.Result = item != null
                ? ModelBindingResult.Success(item)
                : ModelBindingResult.Failed();

            return Task.CompletedTask;
        }
    }
}
