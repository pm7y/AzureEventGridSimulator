using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Dashboard;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end coverage of the dashboard's HTTP surface: the embedded static
///     assets served by DashboardMiddleware, and the JSON API mapped by
///     DashboardEndpoints, whose camelCase property names app.js reads. The
///     fixture (and so the event history) is shared with the other integration
///     tests, so each test looks up the records it created by id or marker
///     instead of assuming an empty history.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class DashboardTests(IntegrationContextFixture factory)
{
    // The topic on this port has only disabled subscribers, so events published
    // to it are recorded in the history without any deliveries.
    private const string TopicWithoutDeliveriesBaseAddress = "https://localhost:60101";
    private const string TopicWithoutDeliveriesName = "ATopicWithATestSubscriber";

    // The DeliveryCatcher subscriber on this topic receives "Deliver.Me" events.
    private const string DeliveryFlowTopicBaseAddress = "https://localhost:60102";

    private const string VendoredDomPurifyFileName = "purify-3.2.7.min.js";

    private HttpClient CreateClient(string baseAddress = TopicWithoutDeliveriesBaseAddress)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri(baseAddress) }
        );
    }

    private HttpClient CreateTopicClient(string baseAddress)
    {
        var client = CreateClient(baseAddress);

        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "TheLocal+DevelopmentKey=");
        client.DefaultRequestHeaders.Add(
            Constants.AegEventTypeHeader,
            Constants.NotificationEventType
        );

        return client;
    }

    private async Task PublishEvent(
        string eventId,
        string baseAddress = TopicWithoutDeliveriesBaseAddress,
        string eventType = "Dashboard.Test"
    )
    {
        var testEvent = new EventGridEvent("/dashboard/test", eventType, "1.0", new { Value = 1 })
        {
            Id = eventId,
        };
        var json = JsonSerializer.Serialize(new[] { testEvent });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await CreateTopicClient(baseAddress).PostAsync("/api/events", content);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, responseBody);
    }

    private async Task<JsonElement> GetJson(string path)
    {
        var response = await CreateClient().GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        var contentType = response.Content.Headers.ContentType;
        contentType.ShouldNotBeNull();
        contentType.MediaType.ShouldBe("application/json");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static byte[] ReadEmbeddedDashboardFile(string fileName)
    {
        var assembly = typeof(DashboardMiddleware).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            $"{assembly.GetName().Name}.Dashboard.{fileName}"
        );
        stream.ShouldNotBeNull($"the dashboard should embed {fileName}");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static async Task ShouldBeServedAsset(
        HttpResponseMessage response,
        string fileName,
        string expectedMediaType
    )
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var contentType = response.Content.Headers.ContentType;
        contentType.ShouldNotBeNull();
        contentType.MediaType.ShouldBe(expectedMediaType);
        contentType.CharSet.ShouldBe("utf-8");

        response.Headers.ETag.ShouldNotBeNull();
        var cacheControl = response.Headers.CacheControl;
        cacheControl.ShouldNotBeNull();
        cacheControl.NoCache.ShouldBeTrue();

        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(
            ReadEmbeddedDashboardFile(fileName)
        );
    }

    private static void ShouldHaveExactlyProperties(JsonElement element, params string[] expected)
    {
        element.ValueKind.ShouldBe(JsonValueKind.Object);
        element.EnumerateObject().Select(p => p.Name).ShouldBe(expected, ignoreOrder: true);
    }

    private static JsonElement FindById(JsonElement array, string id)
    {
        array.ValueKind.ShouldBe(JsonValueKind.Array);
        return array
            .EnumerateArray()
            .Single(e =>
                string.Equals(e.GetProperty("id").GetString(), id, StringComparison.Ordinal)
            );
    }

    // Static assets (DashboardMiddleware)

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/dashboard/")]
    [InlineData("/DASHBOARD")]
    public async Task GivenDashboardRootPath_WhenRequested_ThenIndexHtmlServedWithCachingHeaders(
        string path
    )
    {
        var response = await CreateClient().GetAsync(path);

        await ShouldBeServedAsset(response, "index.html", "text/html");
    }

    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("styles.css", "text/css")]
    [InlineData("app.js", "application/javascript")]
    [InlineData(VendoredDomPurifyFileName, "application/javascript")]
    public async Task GivenDashboardAsset_WhenRequested_ThenServedWithContentTypeAndCachingHeaders(
        string fileName,
        string expectedMediaType
    )
    {
        var response = await CreateClient().GetAsync($"/dashboard/{fileName}");

        await ShouldBeServedAsset(response, fileName, expectedMediaType);
    }

    // The dashboard must work offline, so DOMPurify is served by the simulator rather than
    // loaded from a CDN, and it has to load before app.js, which uses it.
    [Fact]
    public async Task GivenDashboardPage_WhenRequested_ThenScriptsAreLoadedFromTheSimulatorOnly()
    {
        var html = await CreateClient().GetStringAsync("/dashboard/");

        html.ShouldNotContain("src=\"http", Case.Insensitive);
        html.ShouldNotContain("href=\"http", Case.Insensitive);

        var purifyTag = $"<script src=\"/dashboard/{VendoredDomPurifyFileName}\"></script>";
        var appTag = "<script src=\"/dashboard/app.js\"></script>";
        html.ShouldContain(purifyTag);
        html.ShouldContain(appTag);
        html.IndexOf(purifyTag, StringComparison.Ordinal)
            .ShouldBeLessThan(html.IndexOf(appTag, StringComparison.Ordinal));
    }

    // The version in the vendored file's name is what tells maintainers which release it is, so
    // it has to agree with the version in the library's own licence banner.
    [Fact]
    public void GivenVendoredDomPurify_WhenInspected_ThenItsBannerMatchesTheVersionInItsFileName()
    {
        var version = VendoredDomPurifyFileName["purify-".Length..^".min.js".Length];

        var contents = Encoding.UTF8.GetString(
            ReadEmbeddedDashboardFile(VendoredDomPurifyFileName)
        );

        contents.ShouldStartWith($"/*! @license DOMPurify {version} ");
    }

    [Fact]
    public async Task GivenMatchingIfNoneMatch_WhenAssetRequestedAgain_ThenNotModified()
    {
        var client = CreateClient();
        var first = await client.GetAsync("/dashboard/styles.css");
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = first.Headers.ETag;
        etag.ShouldNotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/dashboard/styles.css");
        request.Headers.IfNoneMatch.Add(etag);
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenStaleIfNoneMatch_WhenAssetRequested_ThenServedInFull()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/dashboard/styles.css");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"some-older-build\""));

        var response = await CreateClient().SendAsync(request);

        await ShouldBeServedAsset(response, "styles.css", "text/css");
    }

    // The resource path is everything after "/dashboard/", so an extra slash
    // doesn't resolve to an asset.
    [Theory]
    [InlineData("/dashboard/missing.js")]
    [InlineData("/dashboard//styles.css")]
    public async Task GivenUnknownAsset_WhenRequested_ThenNotFound(string path)
    {
        var response = await CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // HttpClient normalises "/../" before the request is sent, so the traversal
    // guard is reached with a ".." inside a segment or an encoded backslash.
    [Theory]
    [InlineData("/dashboard/a..b.js")]
    [InlineData("/dashboard/a%5Cb.js")]
    public async Task GivenPathTraversalAttempt_WhenRequested_ThenBadRequest(string path)
    {
        var response = await CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GivenDashboardApiPath_WhenRequested_ThenHandledByTheApiEndpointNotTheAssetMiddleware()
    {
        var stats = await GetJson("/dashboard/api/stats");

        stats.TryGetProperty("totalEventsReceived", out _).ShouldBeTrue();
    }

    // JSON API (DashboardEndpoints)

    [Fact]
    public async Task GivenPublishedEvent_WhenEventsListed_ThenSummaryUsesTheCamelCaseContractTheUiReads()
    {
        var eventId = Guid.NewGuid().ToString();
        await PublishEvent(eventId);

        var summary = FindById(await GetJson("/dashboard/api/events"), eventId);

        ShouldHaveExactlyProperties(
            summary,
            "id",
            "eventType",
            "subject",
            "topicName",
            "receivedAt",
            "deliveries"
        );
        summary.GetProperty("eventType").GetString().ShouldBe("Dashboard.Test");
        summary.GetProperty("subject").GetString().ShouldBe("/dashboard/test");
        summary.GetProperty("topicName").GetString().ShouldBe(TopicWithoutDeliveriesName);
        summary.GetProperty("receivedAt").ValueKind.ShouldBe(JsonValueKind.String);
        summary.GetProperty("deliveries").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    public async Task GivenPublishedEvent_WhenFetchedById_ThenDetailsUseTheCamelCaseContractTheUiReads()
    {
        var eventId = Guid.NewGuid().ToString();
        await PublishEvent(eventId);

        var details = await GetJson($"/dashboard/api/events/{eventId}");

        ShouldHaveExactlyProperties(
            details,
            "id",
            "eventType",
            "subject",
            "source",
            "eventTime",
            "topicName",
            "topicPort",
            "inputSchema",
            "receivedAt",
            "payloadJson",
            "deliveries"
        );
        details.GetProperty("id").GetString().ShouldBe(eventId);
        details.GetProperty("eventType").GetString().ShouldBe("Dashboard.Test");
        details.GetProperty("subject").GetString().ShouldBe("/dashboard/test");
        details.GetProperty("topicName").GetString().ShouldBe(TopicWithoutDeliveriesName);
        details.GetProperty("topicPort").GetInt32().ShouldBe(60101);
        details.GetProperty("inputSchema").GetString().ShouldBe("EventGridSchema");
        var payloadJson = details.GetProperty("payloadJson").GetString();
        payloadJson.ShouldNotBeNull();
        payloadJson.ShouldContain(eventId);
        details.GetProperty("deliveries").GetArrayLength().ShouldBe(0);
    }

    // Event ids can be any non-empty string. app.js requests details with
    // encodeURIComponent(id), which Uri.EscapeDataString matches, and Kestrel decodes the
    // segment back into the route value. The exception is %2F, which Kestrel leaves encoded,
    // so ids containing '/' still can't be looked up.
    [Theory]
    [InlineData("?x=1")]
    [InlineData("%41")]
    [InlineData("#fragment")]
    [InlineData(" with \"quotes\" & spaces")]
    public async Task GivenEventIdWithUrlSignificantCharacters_WhenFetchedByEncodedId_ThenFound(
        string idSuffix
    )
    {
        var eventId = Guid.NewGuid() + idSuffix;
        await PublishEvent(eventId);

        var details = await GetJson($"/dashboard/api/events/{Uri.EscapeDataString(eventId)}");

        details.GetProperty("id").GetString().ShouldBe(eventId);
    }

    [Fact]
    public async Task GivenUnknownEventId_WhenFetchedById_ThenNotFound()
    {
        var response = await CreateClient().GetAsync($"/dashboard/api/events/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GivenEventDeliveredToSubscriber_WhenFetched_ThenDeliveriesUseTheCamelCaseContractTheUiReads()
    {
        var eventId = Guid.NewGuid().ToString();
        await PublishEvent(eventId, DeliveryFlowTopicBaseAddress, "Deliver.Me");

        // Delivery is asynchronous (the background service polls every second),
        // so poll until the DeliveryCatcher delivery has completed.
        JsonElement? delivery = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (delivery is null && DateTimeOffset.UtcNow < deadline)
        {
            var details = await GetJson($"/dashboard/api/events/{eventId}");
            var candidate = details
                .GetProperty("deliveries")
                .EnumerateArray()
                .FirstOrDefault(d =>
                    string.Equals(
                        d.GetProperty("subscriberName").GetString(),
                        "DeliveryCatcher",
                        StringComparison.Ordinal
                    )
                );

            if (
                candidate.ValueKind == JsonValueKind.Object
                && candidate.GetProperty("completedAt").ValueKind != JsonValueKind.Null
            )
            {
                delivery = candidate;
            }
            else
            {
                await Task.Delay(100);
            }
        }

        delivery.ShouldNotBeNull(
            "the DeliveryCatcher delivery did not complete within the timeout"
        );

        ShouldHaveExactlyProperties(
            delivery.Value,
            "subscriberName",
            "subscriberType",
            "endpoint",
            "status",
            "lastAttemptAt",
            "completedAt",
            "attempts"
        );
        delivery.Value.GetProperty("subscriberType").GetString().ShouldBe("http");
        delivery
            .Value.GetProperty("endpoint")
            .GetString()
            .ShouldBe("https://delivery-catcher.test/events");
        delivery.Value.GetProperty("status").GetString().ShouldBe("Delivered");

        var attempt = delivery.Value.GetProperty("attempts").EnumerateArray().First();
        ShouldHaveExactlyProperties(
            attempt,
            "attemptNumber",
            "attemptTime",
            "outcome",
            "statusCode",
            "errorMessage"
        );
        attempt.GetProperty("attemptNumber").GetInt32().ShouldBe(1);
        attempt.GetProperty("outcome").GetString().ShouldBe("Success");
        attempt.GetProperty("statusCode").GetInt32().ShouldBe(200);

        var summaryDelivery = FindById(await GetJson("/dashboard/api/events"), eventId)
            .GetProperty("deliveries")
            .EnumerateArray()
            .Single(d =>
                string.Equals(
                    d.GetProperty("subscriberName").GetString(),
                    "DeliveryCatcher",
                    StringComparison.Ordinal
                )
            );
        ShouldHaveExactlyProperties(summaryDelivery, "subscriberName", "status");
        summaryDelivery.GetProperty("status").GetString().ShouldBe("Delivered");
    }

    [Fact]
    public async Task GivenPublishedEvent_WhenStatsFetched_ThenStatsUseTheCamelCaseContractTheUiReads()
    {
        var before = await GetJson("/dashboard/api/stats");

        await PublishEvent(Guid.NewGuid().ToString());

        var after = await GetJson("/dashboard/api/stats");
        ShouldHaveExactlyProperties(
            after,
            "totalEventsReceived",
            "eventsInHistory",
            "totalDelivered",
            "totalFailed",
            "totalPending",
            "totalRejected",
            "topicsActive",
            "oldestEventTime",
            "newestEventTime"
        );
        after
            .GetProperty("totalEventsReceived")
            .GetInt32()
            .ShouldBe(before.GetProperty("totalEventsReceived").GetInt32() + 1);
        after.GetProperty("eventsInHistory").GetInt32().ShouldBeGreaterThan(0);
        after.GetProperty("topicsActive").GetInt32().ShouldBeGreaterThan(0);
        after.GetProperty("newestEventTime").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task GivenRejectedRequest_WhenRejectionsListed_ThenRejectionUsesTheCamelCaseContractTheUiReads()
    {
        // An event with only an id fails validation; the marker finds this
        // rejection among those recorded by other tests on the shared fixture.
        var marker = Guid.NewGuid().ToString();
        using var content = new StringContent(
            $$"""[{"id":"{{marker}}"}]""",
            Encoding.UTF8,
            "application/json"
        );
        var response = await CreateTopicClient(TopicWithoutDeliveriesBaseAddress)
            .PostAsync("/api/events", content);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var rejection = (await GetJson("/dashboard/api/rejections"))
            .EnumerateArray()
            .Single(r =>
                r.GetProperty("rawBody").GetString()?.Contains(marker, StringComparison.Ordinal)
                == true
            );

        ShouldHaveExactlyProperties(
            rejection,
            "id",
            "rejectedAt",
            "topicName",
            "topicPort",
            "statusCode",
            "errorMessage",
            "rawBody",
            "contentType"
        );
        rejection.GetProperty("topicName").GetString().ShouldBe(TopicWithoutDeliveriesName);
        rejection.GetProperty("topicPort").GetInt32().ShouldBe(60101);
        rejection.GetProperty("statusCode").GetInt32().ShouldBe(400);
        rejection.GetProperty("errorMessage").GetString().ShouldNotBeNullOrWhiteSpace();
        rejection.GetProperty("contentType").GetString().ShouldStartWith("application/json");
    }

    [Fact]
    public async Task GivenHistory_WhenCleared_ThenStatsAreReset()
    {
        var eventId = Guid.NewGuid().ToString();
        await PublishEvent(eventId);
        (await GetJson("/dashboard/api/stats"))
            .GetProperty("totalEventsReceived")
            .GetInt32()
            .ShouldBeGreaterThan(0);

        var response = await CreateClient().DeleteAsync("/dashboard/api/clear");

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        using (var doc = JsonDocument.Parse(body))
        {
            ShouldHaveExactlyProperties(doc.RootElement, "message");
            doc.RootElement.GetProperty("message").GetString().ShouldBe("History cleared");
        }

        var stats = await GetJson("/dashboard/api/stats");
        stats.GetProperty("totalEventsReceived").GetInt32().ShouldBe(0);
        stats.GetProperty("eventsInHistory").GetInt32().ShouldBe(0);
        stats.GetProperty("totalDelivered").GetInt32().ShouldBe(0);
        stats.GetProperty("totalFailed").GetInt32().ShouldBe(0);
        stats.GetProperty("totalPending").GetInt32().ShouldBe(0);
        stats.GetProperty("totalRejected").GetInt32().ShouldBe(0);
        stats.GetProperty("oldestEventTime").ValueKind.ShouldBe(JsonValueKind.Null);
        stats.GetProperty("newestEventTime").ValueKind.ShouldBe(JsonValueKind.Null);

        (await CreateClient().GetAsync($"/dashboard/api/events/{eventId}")).StatusCode.ShouldBe(
            HttpStatusCode.NotFound
        );
    }
}
