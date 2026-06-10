using System.Text;
using AzureEventGridSimulator.Infrastructure.Extensions;
using Shouldly;
using Xunit;

namespace AzureEventGridSimulator.Tests.UnitTests.Extensions;

[Trait("Category", "unit")]
public class HttpContextExtensionsTests
{
    private const string Body = """{"hello":"world"}""";

    private static DefaultHttpContext CreateContextWithBody(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
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
        // and may be re-read later in the pipeline.
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
}
