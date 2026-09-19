using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using AzureEventGridSimulator.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     End-to-end tests for how the simulator answers a publish that fails SAS authentication:
///     the status code (401, or 400 for an empty key) and the Azure-style error body.
/// </summary>
[Trait("Category", "integration")]
[Collection(nameof(IntegrationContextFixtureCollection))]
public class SasAuthenticationTests(IntegrationContextFixture factory)
{
    // ATopicWithATestSubscriber in appsettings.test.json, keyed with "TheLocal+DevelopmentKey="
    private const int KeyedTopicPort = 60101;

    private HttpClient CreateClient()
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://localhost:{KeyedTopicPort}"),
            }
        );
    }

    private static string CreateValidEventJson()
    {
        var testEvent = new EventGridEvent("subject", "eventType", "1.0", new { Blah = 1 });
        return JsonSerializer.Serialize(new[] { testEvent });
    }

    private static AzureError ParseError(string body)
    {
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("error");

        return new AzureError(
            error.GetProperty("code").GetString().ShouldNotBeNull(),
            error.GetProperty("details")[0].GetProperty("code").GetString().ShouldNotBeNull(),
            error.GetProperty("message").GetString().ShouldNotBeNull()
        );
    }

    [Fact]
    public async Task GivenNoAuthHeader_WhenPosted_ThenUnauthorizedWithMissingSignatureMessage()
    {
        var client = CreateClient();

        using var content = new StringContent(
            CreateValidEventJson(),
            Encoding.UTF8,
            "application/json"
        );
        using var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = ParseError(await response.Content.ReadAsStringAsync());
        error.Code.ShouldBe("Unauthorized");
        error.DetailCode.ShouldBe("Unauthorized");
        error.Message.ShouldStartWith(
            "Request must contain one of the following authorization signature: aeg-sas-token, aeg-sas-key.",
            Case.Sensitive
        );
    }

    [Fact]
    public async Task GivenWrongAegSasKey_WhenPosted_ThenUnauthorizedWithNotAuthorizedMessage()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(Constants.AegSasKeyHeader, "NotTheTopicKey=");

        using var content = new StringContent(
            CreateValidEventJson(),
            Encoding.UTF8,
            "application/json"
        );
        using var response = await client.PostAsync("/api/events", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var error = ParseError(await response.Content.ReadAsStringAsync());
        error.Code.ShouldBe("Unauthorized");
        error.DetailCode.ShouldBe("Unauthorized");
        error.Message.ShouldContain("not authorized for", Case.Sensitive);
    }

    [Fact]
    public async Task GivenEmptyAegSasKey_WhenPosted_ThenBadRequestWithInvalidSas()
    {
        // The in-memory HttpClient handler drops a header whose only value is empty, even one
        // added with TryAddWithoutValidation. So build the request on the server side, with the
        // header present and empty, as the pipeline sees an 'aeg-sas-key:' header with no value.
        var context = await factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Post;
            c.Request.Scheme = "https";
            c.Request.Host = new HostString("localhost", KeyedTopicPort);
            c.Request.Path = "/api/events";
            c.Request.ContentType = "application/json";
            c.Request.Headers[Constants.AegSasKeyHeader] = string.Empty;
            c.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateValidEventJson()));
        });

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        using var reader = new StreamReader(context.Response.Body);
        var error = ParseError(await reader.ReadToEndAsync());
        error.Code.ShouldBe("BadRequest");
        error.DetailCode.ShouldBe("InvalidSas");
        error.Message.ShouldStartWith("Request must have a value for aeg-sas-key.", Case.Sensitive);
    }

    private sealed record AzureError(string Code, string DetailCode, string Message);
}
