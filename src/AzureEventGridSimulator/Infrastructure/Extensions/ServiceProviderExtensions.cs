using System;
using System.Collections.Generic;
using System.Linq;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceProviderExtensions
{
    public static SimulatorSettings SimulatorSettings(this IServiceProvider provider)
    {
        return provider.GetService<SimulatorSettings>();
    }

    public static IEnumerable<TopicSettings> EnabledTopics(this IServiceProvider provider)
    {
        return SimulatorSettings(provider).Topics.Where(o => !o.Disabled);
    }
}
