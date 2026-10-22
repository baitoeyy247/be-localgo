using System.Diagnostics;

namespace LocalGo.Api.Middleware;

/// <summary>
/// L-T2-2: one log line per request — method, path (no query string), status, elapsed ms.
/// Never logs bodies, headers or tokens. Aborted requests are marked so cut-off calls are traceable.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
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
            var path = context.Request.Path.HasValue ? context.Request.Path.Value : "/";
            if (!IsNoise(path!))
            {
                var status = failed && !context.Response.HasStarted ? 500 : context.Response.StatusCode;
                var aborted = context.RequestAborted.IsCancellationRequested ? " aborted" : string.Empty;
                logger.LogInformation(
                    "[REQ] {Method} {Path} {StatusCode} {ElapsedMs}ms{Aborted}",
                    context.Request.Method,
                    path,
                    status,
                    sw.ElapsedMilliseconds,
                    aborted);
            }
        }
    }

    internal static bool IsNoise(string path) =>
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);
}
