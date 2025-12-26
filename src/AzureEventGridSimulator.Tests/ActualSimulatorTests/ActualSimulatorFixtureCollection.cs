using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

[CollectionDefinition(nameof(ActualSimulatorFixtureCollection))]
public class ActualSimulatorFixtureCollection : ICollectionFixture<ActualSimulatorFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition]
}
