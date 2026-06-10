using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Domain.Services.Routing;
using AzureEventGridSimulator.Infrastructure.Settings;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Routing;

[Trait("Category", "unit")]
public class RequestRouterTests
{
    private const int TopicPort = 60101;

    private static RequestRouter CreateRouter(params TopicSettings[] topics)
    {
        return new RequestRouter(
            new SimulatorSettings
            {
                Topics =
                    topics.Length == 0
                        ? [TestHelpers.CreateValidTopicSettings(port: TopicPort)]
                        : topics,
            }
        );
    }

    private static DefaultHttpContext CreateContext(
        string method,
        string path,
        int? port = TopicPort
    )
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = port.HasValue
            ? new HostString("localhost", port.Value)
            : new HostString("localhost");
        return context;
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/API/EVENTS")]
    [InlineData("/Api/Events")]
    [InlineData("/api/events/")]
    public void GivenPostToApiEvents_WhenPathCaseOrTrailingSlashVaries_ThenRoutesToNotification(
        string path
    )
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, path);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Notification);
        result.Topic.ShouldNotBeNullAnd().Name.ShouldBe("TestTopic");
    }

    [Theory]
    [InlineData("/api/event")]
    [InlineData("/api/eventss")]
    [InlineData("/api/events/extra")]
    public void GivenPostToNearMissPath_WhenRouted_ThenNotFound(string path)
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, path);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
    }

    [Fact]
    public void GivenPostToApiEvents_WhenPortHasNoConfiguredTopic_ThenNotFoundWithoutCrashing()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, "/api/events", port: 59999);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
        result.Topic.ShouldBeNull();
    }

    [Fact]
    public void GivenPostToApiEvents_WhenHostHasNoPort_ThenNotFoundWithoutCrashing()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, "/api/events", port: null);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
    }

    [Fact]
    public void GivenMultipleTopics_WhenRouted_ThenTopicIsSelectedByPort()
    {
        var router = CreateRouter(
            TestHelpers.CreateValidTopicSettings(name: "TopicOne", port: 60101),
            TestHelpers.CreateValidTopicSettings(name: "TopicTwo", port: 60102)
        );
        var context = CreateContext(HttpMethods.Post, "/api/events", port: 60102);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Notification);
        result.Topic.ShouldNotBeNullAnd().Name.ShouldBe("TopicTwo");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public void GivenNonPostMethodToApiEvents_WhenRouted_ThenMethodNotAllowed(string method)
    {
        var router = CreateRouter();
        var context = CreateContext(method, "/api/events");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.MethodNotAllowed);
    }

    [Fact]
    public void GivenHeadToApiEvents_WhenRouted_ThenHeadApiEvents()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Head, "/api/events");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.HeadApiEvents);
    }

    [Fact]
    public void GivenOptionsToApiEvents_WhenPortMatchesTopic_ThenOptionsPreFlightWithTopic()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Options, "/api/events");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.OptionsPreFlight);
        result.Topic.ShouldNotBeNull();
    }

    [Fact]
    public void GivenOptionsToApiEvents_WhenPortDoesNotMatch_ThenOptionsPreFlightWithNullTopic()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Options, "/api/events", port: 59999);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.OptionsPreFlight);
        result.Topic.ShouldBeNull();
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/API/HEALTH")]
    public void GivenGetToApiHealth_WhenRouted_ThenHealth(string path)
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, path);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Health);
    }

    [Fact]
    public void GivenGetToValidateWithGuidId_WhenRouted_ThenSubscriptionValidation()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, "/validate");
        context.Request.QueryString = new QueryString($"?id={Guid.NewGuid()}");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.SubscriptionValidation);
    }

    [Fact]
    public void GivenGetToValidateWithNonGuidId_WhenRouted_ThenNotFound()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, "/validate");
        context.Request.QueryString = new QueryString("?id=not-a-guid");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
    }

    [Fact]
    public void GivenGetToValidateWithoutId_WhenRouted_ThenNotFound()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, "/validate");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/dashboard/stats")]
    [InlineData("/DASHBOARD/events")]
    public void GivenDashboardPath_WhenRouted_ThenDashboard(string path)
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, path);

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Dashboard);
    }

    [Fact]
    public void GivenFaviconRequest_WhenRouted_ThenFaviconIgnore()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, "/favicon.ico");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.FaviconIgnore);
    }

    [Fact]
    public void GivenUnknownPath_WhenRouted_ThenNotFound()
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Get, "/some/unknown/path");

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.NotFound);
    }

    [Fact]
    public void GivenPostWithCeHeaderAndNonJsonContentType_WhenRouted_ThenBinaryModeNotification()
    {
        // Any ce-* header puts the request into CloudEvents binary mode regardless of content type
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, "/api/events");
        context.Request.Headers[Constants.CeIdHeader] = "some-id";
        context.Request.ContentType = "text/plain";

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Notification);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    [InlineData("application/cloudevents+json")]
    [InlineData("application/cloudevents-batch+json")]
    [InlineData("text/plain")]
    public void GivenPostToApiEvents_WhenContentTypeIsLenientlyAccepted_ThenNotification(
        string? contentType
    )
    {
        var router = CreateRouter();
        var context = CreateContext(HttpMethods.Post, "/api/events");
        context.Request.ContentType = contentType;

        var result = router.RouteRequest(context);

        result.Type.ShouldBe(RequestType.Notification);
    }
}
