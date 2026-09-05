using AzureEventGridSimulator.Infrastructure.Settings;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Management;

[Trait("Category", "unit")]
public class ManagementPortValidationTests
{
    private static SimulatorSettings SettingsWith(int topicPort, int? managementPort) =>
        new()
        {
            DashboardEnabled = false,
            ManagementPort = managementPort,
            Topics = [new TopicSettings { Name = "Topic", Port = topicPort }],
        };

    [Fact]
    public void Should_Throw_When_ManagementPortClashesWithTopicPort()
    {
        var settings = SettingsWith(topicPort: 60101, managementPort: 60101);

        Should
            .Throw<InvalidOperationException>(() => settings.Validate())
            .Message.ShouldContain("management port");
    }

    [Fact]
    public void Should_Pass_When_ManagementPortIsDistinct()
    {
        var settings = SettingsWith(topicPort: 60101, managementPort: 60100);

        Should.NotThrow(() => settings.Validate());
    }
}
