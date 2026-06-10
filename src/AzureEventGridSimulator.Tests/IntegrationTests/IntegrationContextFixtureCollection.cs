using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     Shares a single WebApplicationFactory across all integration test classes.
///     Two in-process hosts of the simulator cannot start concurrently, so the
///     classes must share one factory (and run sequentially within the collection).
/// </summary>
[CollectionDefinition(nameof(IntegrationContextFixtureCollection))]
public class IntegrationContextFixtureCollection : ICollectionFixture<IntegrationContextFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition]
}
