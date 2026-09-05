using System.Net;
using System.Text;
using System.Text.Json;

namespace AzureEventGridSimulator.Tests.Helpers;

/// <summary>
///     A minimal in-process webhook endpoint used by integration tests. It automatically completes
///     the Event Grid subscription-validation handshake (echoing the validation code back) and
///     records the bodies of any events delivered to it.
/// </summary>
public sealed class WebhookSink : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly List<string> _receivedEvents = [];

    private WebhookSink(int port)
    {
        Endpoint = $"http://localhost:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(Endpoint);
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public string Endpoint { get; }

    public IReadOnlyList<string> ReceivedEvents
    {
        get
        {
            lock (_receivedEvents)
            {
                return _receivedEvents.ToArray();
            }
        }
    }

    public static WebhookSink Start(int port) => new(port);

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            using var reader = new StreamReader(
                context.Request.InputStream,
                context.Request.ContentEncoding
            );
            var body = await reader.ReadToEndAsync();

            var eventType = context.Request.Headers["aeg-event-type"];
            var responseText = "";

            if (
                string.Equals(
                    eventType,
                    "SubscriptionValidation",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                responseText = $"{{\"validationResponse\":\"{ExtractValidationCode(body)}\"}}";
            }
            else
            {
                lock (_receivedEvents)
                {
                    _receivedEvents.Add(body);
                }
            }

            var buffer = Encoding.UTF8.GetBytes(responseText);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.Close();
        }
    }

    private static string ExtractValidationCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var evt = root.ValueKind == JsonValueKind.Array ? root[0] : root;
        return evt.GetProperty("data").GetProperty("validationCode").GetString() ?? "";
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        _listener.Close();

        try
        {
            await _acceptLoop;
        }
        catch
        {
            // Ignore shutdown races.
        }

        _cts.Dispose();
    }
}
