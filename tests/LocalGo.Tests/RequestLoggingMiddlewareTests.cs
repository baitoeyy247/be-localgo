using LocalGo.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LocalGo.Tests;

public class RequestLoggingMiddlewareTests
{
    private sealed class CaptureLogger : ILogger<RequestLoggingMiddleware>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }

    [Fact]
    public async Task Logs_one_line_with_method_path_status_and_ms_without_query()
    {
        var logger = new CaptureLogger();
        var mw = new RequestLoggingMiddleware(ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; }, logger);
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "PATCH";
        ctx.Request.Path = "/api/appointments/abc";
        ctx.Request.QueryString = new QueryString("?status=Confirmed&token=secret");
        await mw.InvokeAsync(ctx);
        var line = Assert.Single(logger.Lines);
        Assert.StartsWith("[REQ] PATCH /api/appointments/abc 200 ", line);
        Assert.EndsWith("ms", line);
        Assert.DoesNotContain("secret", line);
    }

    [Fact]
    public async Task Skips_health_checks()
    {
        var logger = new CaptureLogger();
        var mw = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/health";
        await mw.InvokeAsync(ctx);
        Assert.Empty(logger.Lines);
    }
}
