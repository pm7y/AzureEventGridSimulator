using System.Globalization;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Infrastructure.Settings.Subscribers;
using Microsoft.Extensions.Primitives;

namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSimulatorSettings(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = new SimulatorSettings();
        configuration.Bind(settings);
        BindShapesIgnoredByConfigurationBinder(settings, configuration);
        settings.Validate();
        services.AddSingleton(_ => settings);

        return services;
    }

    /// <summary>
    ///     ConfigurationBinder ignores the System.Text.Json converters on the settings classes, so
    ///     bind the two shapes it gets wrong the way JsonSerializer would:
    ///     <list type="bullet">
    ///         <item>
    ///             the legacy <c>"subscribers": [ ... ]</c> array (keys "0", "1", ...) matches no
    ///             property and would be silently dropped, so bind it to HTTP subscribers as
    ///             <see cref="SubscribersSettingsConverter" /> does. A subscribers section that also
    ///             has grouped keys (http, serviceBus, ...), e.g. from layered config files, is
    ///             treated as the grouped format.
    ///         </item>
    ///         <item>
    ///             NumberInRange / NumberNotInRange <c>[min, max]</c> pairs in the
    ///             <c>ICollection&lt;object&gt;</c> filter values would bind to bare objects.
    ///         </item>
    ///     </list>
    /// </summary>
    private static void BindShapesIgnoredByConfigurationBinder(
        SimulatorSettings settings,
        IConfiguration configuration
    )
    {
        foreach (
            var (topic, topicSection) in PairWithSections(
                settings.Topics,
                configuration.GetSection("topics")
            )
        )
        {
            var subscribers = topic.Subscribers;
            var subscribersSection = topicSection.GetSection("subscribers");
            var children = subscribersSection.GetChildren().ToList();

            if (children.Count > 0 && children.TrueForAll(c => IsArrayIndex(c.Key)))
            {
                subscribers.Http = subscribersSection.Get<HttpSubscriberSettings[]>();
                BindRangeFilterValues(subscribers.Http, subscribersSection);
                continue;
            }

            BindRangeFilterValues(subscribers.Http, subscribersSection.GetSection("http"));
            BindRangeFilterValues(
                subscribers.ServiceBus,
                subscribersSection.GetSection("serviceBus")
            );
            BindRangeFilterValues(
                subscribers.StorageQueue,
                subscribersSection.GetSection("storageQueue")
            );
            BindRangeFilterValues(subscribers.EventHub, subscribersSection.GetSection("eventHub"));
        }
    }

    private static void BindRangeFilterValues<TSubscriber>(
        IEnumerable<TSubscriber>? subscribers,
        IConfigurationSection subscriberArraySection
    )
        where TSubscriber : ISubscriberSettings
    {
        foreach (
            var (subscriber, subscriberSection) in PairWithSections(
                subscribers,
                subscriberArraySection
            )
        )
        {
            foreach (
                var (advancedFilter, advancedFilterSection) in PairWithSections(
                    subscriber.Filter?.AdvancedFilters,
                    subscriberSection.GetSection("filter:advancedFilters")
                )
            )
            {
                var valueSections = advancedFilterSection
                    .GetSection("values")
                    .GetChildren()
                    .ToList();

                if (
                    advancedFilter.Values is not { } values
                    || !valueSections.Exists(v => v.GetChildren().Any())
                )
                {
                    continue;
                }

                // Rebuild the bound list, replacing each nested pair with object[] { min, max }
                values.Clear();
                foreach (var valueSection in valueSections)
                {
                    var range = valueSection.GetChildren().Select(c => (object?)c.Value).ToArray();

                    if (range.Length > 0)
                    {
                        values.Add(range);
                    }
                    else if (valueSection.Value is not null)
                    {
                        values.Add(valueSection.Value);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Pairs each bound item with the configuration section it was bound from.
    ///     ConfigurationBinder silently skips a collection element it cannot bind (an unknown enum
    ///     name, a non-numeric port, a scalar where an object is expected, ...), so the sections of
    ///     skipped elements are left out; pairing by position would give every item after a
    ///     skipped element its neighbour's section. If the items and sections still don't line
    ///     up, returns no pairs so the bound items are left as the binder produced them.
    /// </summary>
    private static IEnumerable<(T, IConfigurationSection)> PairWithSections<T>(
        IEnumerable<T>? items,
        IConfigurationSection collectionSection
    )
    {
        var bound = items?.ToList() ?? [];
        var sections = collectionSection.GetChildren().Where(IsKeptByBinder<T>).ToList();

        return bound.Count == sections.Count ? bound.Zip(sections) : [];
    }

    /// <summary>
    ///     Whether ConfigurationBinder keeps this element when it binds the element's collection.
    ///     Binds the element on its own as a one-element array, so the binder applies its own
    ///     per-element rule rather than a copy of it.
    /// </summary>
    private static bool IsKeptByBinder<T>(IConfigurationSection element)
    {
        return new SingleChildConfiguration(element).Get<T[]>() is { Length: 1 };
    }

    private static bool IsArrayIndex(string key)
    {
        return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    ///     A configuration node whose only child is <c>child</c>. Binding it to an array only
    ///     reads its children, so nothing else is supported.
    /// </summary>
    private sealed class SingleChildConfiguration(IConfigurationSection child) : IConfiguration
    {
        public string? this[string key]
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return [child];
        }

        public IChangeToken GetReloadToken()
        {
            return child.GetReloadToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            throw new NotSupportedException();
        }
    }
}
