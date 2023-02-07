namespace AzureEventGridSimulator.Infrastructure.ModelBinders;

using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class FromFeaturesAttribute : Attribute, IBindingSourceMetadata
{
    public BindingSource BindingSource => CustomBindingSources.HttpContextFeatures;
}
