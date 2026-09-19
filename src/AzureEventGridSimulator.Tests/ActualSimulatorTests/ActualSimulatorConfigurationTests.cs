using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.ActualSimulatorTests;

/// <summary>
///     Checks which configuration the compiled simulator started with. Only the test
///     configuration may be loaded: the shipped appsettings.json has legacy subscribers that
///     deliver to requestcatcher.com, and layering it under appsettings.test.json would merge
///     them into the test topics by index.
/// </summary>
[Collection(nameof(ActualSimulatorFixtureCollection))]
[Trait("Category", "integration-actual")]
public class ActualSimulatorConfigurationTests(ActualSimulatorFixture fixture)
{
    [Fact]
    public async Task GivenTheCompiledSimulator_WhenStarted_ThenOnlyTheTestConfigurationIsLoaded()
    {
        // The topic summary is logged after the subscription validation sweep
        var output = await fixture.WaitForOutput("It's alive !", TimeSpan.FromSeconds(30));

        output.ShouldContain("Topic 'CloudEventsTopic' (port 60104) has 0 subscriber(s)");
        output.ShouldNotContain("CloudEventsRequestCatcherSubscription");
    }
}
