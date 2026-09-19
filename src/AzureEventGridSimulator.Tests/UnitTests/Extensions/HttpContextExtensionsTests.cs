using System.Net;
using System.Text;
using System.Text.Json;
using AzureEventGridSimulator.Domain.Entities;
using AzureEventGridSimulator.Infrastructure;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Extensions;

[Trait("Category", "unit")]
public class HttpContextExtensionsTests
{
    private const string Body = """{"hello":"world"}""";

    // 14:07:09 UTC, so the report suffix shows the 12-hour clock, the PM designator and the
    // unpadded month/day/hour
    private static readonly DateTimeOffset FixedUtcNow = new(2025, 1, 5, 14, 7, 9, TimeSpan.Zero);

    private static DefaultHttpContext CreateContextWithBody(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }

    private static DefaultHttpContext CreateContextForErrorResponse()
    {
        // RequestServices is null on a DefaultHttpContext, and GenerateReportSuffix would then
        // fall back to the wall clock
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<TimeProvider>(new FakeTimeProvider(FixedUtcNow))
                .BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static JsonElement ReadError(HttpContext context)
    {
        using var document = JsonDocument.Parse(ReadResponseBody(context));
        return document.RootElement.GetProperty("error").Clone();
    }

    [Fact]
    public async Task GivenRequestBody_WhenReadTwice_ThenBothReadsReturnFullContent()
    {
        var context = CreateContextWithBody(Body);

        var firstRead = await context.RequestBody();
        var secondRead = await context.RequestBody();

        firstRead.ShouldBe(Body);
        secondRead.ShouldBe(Body);
    }

    [Fact]
    public async Task GivenRequestBody_WhenRead_ThenUnderlyingStreamIsNotDisposed()
    {
        // The reader must leave the stream open - it's owned by ASP.NET Core
        var context = CreateContextWithBody(Body);

        _ = await context.RequestBody();

        context.Request.Body.CanRead.ShouldBeTrue();
        context.Request.Body.CanSeek.ShouldBeTrue();
    }

    [Fact]
    public async Task GivenStreamPositionAtEnd_WhenRead_ThenFullBodyIsStillReturned()
    {
        var context = CreateContextWithBody(Body);
        context.Request.Body.Position = context.Request.Body.Length;

        var body = await context.RequestBody();

        body.ShouldBe(Body);
    }

    [Fact]
    public async Task GivenNonSeekableRequestBody_WhenRead_ThenFullBodyIsReturned()
    {
        // Kestrel's request body stream can't seek, and throws if its Position is set
        var context = new DefaultHttpContext();
        context.Request.Body = new NonSeekableStream(Encoding.UTF8.GetBytes(Body));

        var body = await context.RequestBody();

        body.ShouldBe(Body);
    }

    [Fact]
    public void GivenSameContext_WhenRequestIdIsReadTwice_ThenTheSameIdIsReturned()
    {
        var context = new DefaultHttpContext();

        var first = context.GetRequestId();
        var second = context.GetRequestId();

        first.ShouldNotBe(Guid.Empty);
        second.ShouldBe(first);
    }

    [Fact]
    public void GivenTimeProvider_WhenReportSuffixIsGenerated_ThenItMatchesAzureFormatExactly()
    {
        var context = CreateContextForErrorResponse();

        var suffix = context.GenerateReportSuffix();

        suffix.ShouldBe(
            $" Report '{context.GetRequestId()}:1:1/5/2025 2:07:09 PM (UTC)' to our forums for assistance or raise a support ticket."
        );
    }

    [Fact]
    public async Task GivenNoCodes_WhenErrorResponseIsWritten_ThenCodeAndDetailCodeFallBackToStatusName()
    {
        var context = CreateContextForErrorResponse();

        await context.WriteErrorResponse(HttpStatusCode.BadRequest, "Something failed.", null);

        context.Response.StatusCode.ShouldBe(400);
        var error = ReadError(context);
        error.GetProperty("code").GetString().ShouldBe("BadRequest");
        error.GetProperty("message").GetString().ShouldBe("Something failed.");
        var details = error.GetProperty("details");
        details.GetArrayLength().ShouldBe(1);
        details[0].GetProperty("code").GetString().ShouldBe("BadRequest");
        details[0].GetProperty("message").GetString().ShouldBe("Something failed.");
    }

    [Fact]
    public async Task GivenDetailCode_WhenErrorResponseIsWritten_ThenOnlyTheDetailUsesIt()
    {
        var context = CreateContextForErrorResponse();

        await context.WriteErrorResponse(
            HttpStatusCode.BadRequest,
            "Something failed.",
            null,
            ErrorDetailCodes.InputJsonInvalid
        );

        var error = ReadError(context);
        error.GetProperty("code").GetString().ShouldBe("BadRequest");
        error
            .GetProperty("details")[0]
            .GetProperty("code")
            .GetString()
            .ShouldBe(ErrorDetailCodes.InputJsonInvalid);
    }

    [Fact]
    public async Task GivenCodeWithoutDetailCode_WhenErrorResponseIsWritten_ThenDetailCodeFallsBackToCode()
    {
        var context = CreateContextForErrorResponse();

        await context.WriteErrorResponse(HttpStatusCode.Unauthorized, "Nope.", "CustomCode");

        context.Response.StatusCode.ShouldBe(401);
        var error = ReadError(context);
        error.GetProperty("code").GetString().ShouldBe("CustomCode");
        error.GetProperty("details")[0].GetProperty("code").GetString().ShouldBe("CustomCode");
    }

    [Fact]
    public async Task GivenErrorResponse_WhenWritten_ThenAzureHeadersAreSetAndNoContentType()
    {
        var context = CreateContextForErrorResponse();

        await context.WriteErrorResponse(HttpStatusCode.NotFound, "Missing.", null);

        context.Response.Headers["api-supported-versions"].ToString().ShouldBe("2018-01-01");
        context
            .Response.Headers["x-ms-request-id"]
            .ToString()
            .ShouldBe(context.GetRequestId().ToString());
        context.Response.ContentType.ShouldBeNull();
    }

    [Fact]
    public async Task GivenReportSuffixInMessage_WhenErrorResponseIsWritten_ThenRequestIdHeaderMatchesSuffixId()
    {
        var context = CreateContextForErrorResponse();
        var message = $"Something failed.{context.GenerateReportSuffix()}";

        await context.WriteErrorResponse(HttpStatusCode.BadRequest, message, null);

        var requestId = context.Response.Headers["x-ms-request-id"].ToString();
        ReadError(context).GetProperty("message").GetString().ShouldBe(message);
        message.ShouldContain($"Report '{requestId}:1:");
    }

    [Fact]
    public async Task GivenErrorResponse_WhenWritten_ThenBodyIsIndentedWithFourSpacesAndQuotesAreNotEscaped()
    {
        var context = CreateContextForErrorResponse();

        await context.WriteErrorResponse(
            HttpStatusCode.BadRequest,
            "Value 'x' <is> & bad.",
            null,
            ErrorDetailCodes.InputJsonInvalid
        );

        const string expected = """
            {
                "error": {
                    "code": "BadRequest",
                    "message": "Value 'x' <is> & bad.",
                    "details": [
                        {
                            "code": "InputJsonInvalid",
                            "message": "Value 'x' <is> & bad."
                        }
                    ]
                }
            }
            """;
        ReadResponseBody(context)
            .ReplaceLineEndings("\n")
            .ShouldBe(expected.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void GivenValidatedPublish_WhenReadBack_ThenTopicEventsAndSchemaAreTheOnesStored()
    {
        var context = new DefaultHttpContext();
        var topic = TestHelpers.CreateValidTopicSettings();
        SimulatorEvent[] events = [TestHelpers.CreateSimulatorEventFromCloudEvent()];

        context.SetValidatedPublish(topic, events, EventSchema.CloudEventV1_0);
        var publish = context.GetValidatedPublish();

        publish.Topic.ShouldBeSameAs(topic);
        publish.Events.ShouldBeSameAs(events);
        publish.Schema.ShouldBe(EventSchema.CloudEventV1_0);
    }

    [Fact]
    public void GivenNoValidatedPublish_WhenRead_ThenThrowsNamingTheMiddlewareThatStoresIt()
    {
        var context = new DefaultHttpContext();

        var exception = Should.Throw<InvalidOperationException>(() =>
            context.GetValidatedPublish()
        );

        exception.Message.ShouldContain("EventGridMiddleware");
    }

    /// <summary>
    ///     A read-only stream that, like Kestrel's request body, can't seek.
    /// </summary>
    private sealed class NonSeekableStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
