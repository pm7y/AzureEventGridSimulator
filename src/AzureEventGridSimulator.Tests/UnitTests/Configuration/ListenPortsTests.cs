using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Configuration;

[Trait("Category", "unit")]
public class ListenPortsTests
{
    private static TopicSettings CreateTopic(string name, int port, bool disabled = false)
    {
        return new TopicSettings
        {
            Name = name,
            Port = port,
            Key = "TheLocal+DevelopmentKey=",
            Disabled = disabled,
        };
    }

    [Fact]
    public void GivenEnabledAndDisabledTopics_WhenGettingListenPorts_ThenOnlyEnabledTopicPortsAreReturned()
    {
        var settings = new SimulatorSettings
        {
            Topics =
            [
                CreateTopic("topic-one", 60101),
                CreateTopic("topic-two", 60102, disabled: true),
                CreateTopic("topic-three", 60103),
            ],
        };

        Program.GetListenPorts(settings).ShouldBe([60101, 60103], ignoreOrder: true);
    }

    [Fact]
    public void GivenADashboardPort_WhenGettingListenPorts_ThenTheDashboardPortIsAlsoReturned()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101)],
            DashboardEnabled = true,
            DashboardPort = 60200,
        };

        Program.GetListenPorts(settings).ShouldBe([60101, 60200], ignoreOrder: true);
    }

    [Fact]
    public void GivenADashboardPortAndAllTopicsDisabled_WhenGettingListenPorts_ThenOnlyTheDashboardPortIsReturned()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101, disabled: true)],
            DashboardEnabled = true,
            DashboardPort = 60200,
        };

        Program.GetListenPorts(settings).ShouldBe([60200]);
    }

    [Fact]
    public void GivenADashboardPortSharedWithAnEnabledTopic_WhenGettingListenPorts_ThenThePortIsOnlyReturnedOnce()
    {
        // Listening twice on the same port would fail at startup
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101)],
            DashboardEnabled = true,
            DashboardPort = 60101,
        };

        Program.GetListenPorts(settings).ShouldBe([60101]);
    }

    [Fact]
    public void GivenADashboardPortButTheDashboardIsDisabled_WhenGettingListenPorts_ThenTheDashboardPortIsNotReturned()
    {
        var settings = new SimulatorSettings
        {
            Topics = [CreateTopic("topic-one", 60101)],
            DashboardEnabled = false,
            DashboardPort = 60200,
        };

        Program.GetListenPorts(settings).ShouldBe([60101]);
    }
}
