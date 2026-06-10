using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     A captured outbound HTTP request made by the simulator.
/// </summary>
public sealed class CapturedRequest
{
    public required string Method { get; init; }

    public required string Url { get; init; }

    public required string Body { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}

/// <summary>
///     Replaces the primary handler of the simulator's named HttpClient so
///     integration tests can observe outbound deliveries without any network
///     access. Acts as a fake subscriber: requests to the echo-handshaker.test
///     host get the subscription validation code echoed back (the synchronous
///     handshake); every other request gets a plain 200.
/// </summary>
public sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<CapturedRequest> _requests = new();

    public IReadOnlyList<CapturedRequest> Requests => _requests.ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var body =
            request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

        var headers = request
            .Headers.Concat(
                request.Content?.Headers
                    ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>()
            )
            .ToDictionary(
                h => h.Key,
                h => string.Join(",", h.Value),
                StringComparer.OrdinalIgnoreCase
            );

        _requests.Enqueue(
            new CapturedRequest
            {
                Method = request.Method.Method,
                Url = request.RequestUri?.ToString() ?? "",
                Body = body,
                Headers = headers,
            }
        );

        if (
            string.Equals(
                request.RequestUri?.Host,
                "echo-handshaker.test",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return CreateValidationEchoResponse(body);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("", Encoding.UTF8),
        };
    }

    private static HttpResponseMessage CreateValidationEchoResponse(string body)
    {
        // Behave like a well-implemented subscriber: read the validation code from
        // the SubscriptionValidation event and echo it back synchronously.
        using var doc = JsonDocument.Parse(body);
        var validationCode = doc.RootElement[0]
            .GetProperty("data")
            .GetProperty("validationCode")
            .GetGuid();

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"validationResponse":"{{validationCode}}"}""",
                Encoding.UTF8,
                "application/json"
            ),
        };
    }
}
