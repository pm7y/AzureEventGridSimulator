using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain;

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
///     access. Acts as a fake subscriber: subscription validation events sent to
///     the echo-handshaker.test host get the validation code echoed back (the
///     synchronous handshake); requests to the stalling-subscriber.test host get
///     no answer while a test holds it (see <see cref="HoldStallingSubscriber" />);
///     every other request, including ordinary event deliveries to those hosts,
///     gets a plain 200.
/// </summary>
public sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    ///     The host of StallingSubscriber in appsettings.test.json.
    /// </summary>
    public const string StallingSubscriberHost = "stalling-subscriber.test";

    private readonly ConcurrentQueue<CapturedRequest> _requests = new();

    private Task _stallingSubscriberReleased = Task.CompletedTask;

    public IReadOnlyList<CapturedRequest> Requests => _requests.ToArray();

    /// <summary>
    ///     Captures each request to <see cref="StallingSubscriberHost" /> but holds back its
    ///     response until the returned hold is disposed, or until the simulator gives up on
    ///     the request (its client times out after 60 seconds).
    /// </summary>
    public IDisposable HoldStallingSubscriber()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _stallingSubscriberReleased, release.Task);
        return new StallingSubscriberHold(release);
    }

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
                StallingSubscriberHost,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            await Volatile.Read(ref _stallingSubscriberReleased).WaitAsync(cancellationToken);
        }

        if (
            string.Equals(
                request.RequestUri?.Host,
                "echo-handshaker.test",
                StringComparison.OrdinalIgnoreCase
            )
            && headers.TryGetValue(Constants.AegEventTypeHeader, out var eventType)
            && string.Equals(eventType, Constants.ValidationEventType, StringComparison.Ordinal)
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

    private sealed class StallingSubscriberHold(TaskCompletionSource release) : IDisposable
    {
        public void Dispose()
        {
            release.TrySetResult();
        }
    }
}
