using System.Diagnostics;
using System.Globalization;

namespace LocalGo.Api.Middleware;

/// <summary>
/// L-T2-2 / R40: one log line per request — method, path (no query string), status, elapsed ms, request id.
/// Sets X-Request-Id and Server-Timing (app;dur=NN) so the SIT console log can match browser ↔ server.
/// Never logs bodies, headers or tokens. Aborted requests are marked so cut-off calls are traceable.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        if (IsNoise(path))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        var timing = LocalGo.Application.Common.ServerTiming.Begin();
        var rid = Guid.NewGuid().ToString("N")[..12];
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Request-Id"] = rid;
            // P-1: split app into load / save / notify.
            var app = sw.Elapsed.TotalMilliseconds;
            var save = timing.SaveMs;
            var notify = timing.NotifyMs;
            var load = Math.Max(0, app - save - notify);
            string F(double v) => v.ToString("0", CultureInfo.InvariantCulture);
            context.Response.Headers["Server-Timing"] =
                $"app;dur={F(app)}, load;dur={F(load)}, save;dur={F(save)}, notify;dur={F(notify)}";
            return Task.CompletedTask;
        });

        var failed = false;
        try
        {
            await next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            sw.Stop();
            var status = failed && !context.Response.HasStarted ? 500 : context.Response.StatusCode;
            var aborted = context.RequestAborted.IsCancellationRequested ? " aborted" : string.Empty;
            logger.LogInformation(
                "[REQ] {Method} {Path} {StatusCode} {ElapsedMs}ms rid={RequestId}{Aborted}",
                context.Request.Method,
                path,
                status,
                sw.ElapsedMilliseconds,
                rid,
                aborted);
        }
    }

    internal static bool IsNoise(string path) =>
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
