using System.Net;
using System.Text.Json;
using AzureEventGridSimulator.Infrastructure.Extensions;
using AzureEventGridSimulator.Tests.UnitTests.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.IntegrationTests;

/// <summary>
///     The response the simulator writes when a request throws an exception nothing else handles:
///     an Azure-style 500 error body instead of an empty 500 or the developer exception page.
///     Requests Kestrel itself rejects (BadHttpRequestException) keep Kestrel's status.
/// </summary>
[Trait("Category", "integration")]
public class UnhandledExceptionTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 15, 4, 5, TimeSpan.Zero);

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<TimeProvider>(new FakeTimeProvider(Now))
                .BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    private static JsonElement ReadError(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = JsonDocument.Parse(context.Response.Body);
        return document.RootElement.GetProperty("error").Clone();
    }

    /// <summary>
    ///     Starts an app with the same exception handler registration Program.Main makes, in front
    ///     of an endpoint that throws the given exception.
    /// </summary>
    private static async Task<WebApplication> StartAppThatThrows(Exception exception)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.UseExceptionHandler(
            new ExceptionHandlerOptions
            {
                ExceptionHandler = Program.WriteUnhandledExceptionResponse,
            }
        );
        app.Run(_ => throw exception);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task GivenUnhandledException_WhenResponseWritten_ThenStatusIsInternalServerError()
    {
        var context = CreateContext();

        await Program.WriteUnhandledExceptionResponse(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task GivenUnhandledException_WhenResponseWritten_ThenBodyIsAzureStyleError()
    {
        var context = CreateContext();

        await Program.WriteUnhandledExceptionResponse(context);

        var error = ReadError(context);
        error.GetProperty("code").GetString().ShouldBe("InternalServerError");
        error.GetProperty("details").GetArrayLength().ShouldBe(1);
        error
            .GetProperty("details")[0]
            .GetProperty("code")
            .GetString()
            .ShouldBe("InternalServerError");
        error
            .GetProperty("message")
            .GetString()
            .ShouldBe(
                "The simulator encountered an unexpected error while processing the request."
                    + $" Report '{context.GetRequestId()}:1:1/2/2030 3:04:05 PM (UTC)'"
                    + " to our forums for assistance or raise a support ticket."
            );
    }

    [Fact]
    public async Task GivenUnhandledException_WhenResponseWritten_ThenAzureHeadersAreSet()
    {
        var context = CreateContext();

        await Program.WriteUnhandledExceptionResponse(context);

        context.Response.Headers["api-supported-versions"].ToString().ShouldBe("2018-01-01");
        context
            .Response.Headers["x-ms-request-id"]
            .ToString()
            .ShouldBe(context.GetRequestId().ToString());
    }

    [Fact]
    public async Task GivenUnhandledExceptionThatIsNotARequestRejection_WhenResponseWritten_ThenStatusIsInternalServerError()
    {
        var context = CreateContext();
        context.Features.Set<IExceptionHandlerFeature>(
            new ExceptionHandlerFeature { Error = new InvalidOperationException("Unexpected") }
        );

        await Program.WriteUnhandledExceptionResponse(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ReadError(context).GetProperty("code").GetString().ShouldBe("InternalServerError");
    }

    [Fact]
    public async Task GivenKestrelRequestRejection_WhenResponseWritten_ThenItsStatusIsKeptWithNoBody()
    {
        // Kestrel throws BadHttpRequestException for requests it rejects (such as a body over
        // its 30MB MaxRequestBodySize). Kestrel answered those itself before the exception
        // handler was added, with the exception's status and an empty body.
        var context = CreateContext();
        context.Features.Set<IExceptionHandlerFeature>(
            new ExceptionHandlerFeature
            {
                Error = new BadHttpRequestException(
                    "Request body too large.",
                    StatusCodes.Status413PayloadTooLarge
                ),
            }
        );

        await Program.WriteUnhandledExceptionResponse(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        context.Response.Body.Length.ShouldBe(0);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest)] // e.g. the body ended before Content-Length
    [InlineData(StatusCodes.Status408RequestTimeout)] // the body arrived too slowly
    [InlineData(StatusCodes.Status413PayloadTooLarge)] // the body is over MaxRequestBodySize
    public async Task GivenRequestThatKestrelRejects_WhenHandledByExceptionHandler_ThenKestrelStatusIsReturned(
        int statusCode
    )
    {
        // A 500 here would also make Azure SDK clients retry a request that can never succeed
        await using var app = await StartAppThatThrows(
            new BadHttpRequestException("Kestrel rejected the request.", statusCode)
        );

        using var response = await app.GetTestClient().GetAsync("/api/events");

        ((int)response.StatusCode).ShouldBe(statusCode);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenRequestThatThrows_WhenHandledByExceptionHandler_ThenAzureStyleErrorIsReturned()
    {
        await using var app = await StartAppThatThrows(
            new InvalidOperationException("Something the simulator didn't expect")
        );

        using var response = await app.GetTestClient().GetAsync("/api/events");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Headers.GetValues("api-supported-versions").ShouldBe(["2018-01-01"]);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain("Something the simulator didn't expect");
        using var document = JsonDocument.Parse(body);
        document
            .RootElement.GetProperty("error")
            .GetProperty("code")
            .GetString()
            .ShouldBe("InternalServerError");
    }
}
