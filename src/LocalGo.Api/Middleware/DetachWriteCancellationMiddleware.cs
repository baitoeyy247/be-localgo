namespace LocalGo.Api.Middleware;

/// <summary>
/// TX-2 (R41): write requests (POST/PUT/PATCH/DELETE) must not depend on the client staying connected.
/// Swaps HttpContext.RequestAborted for an independent 30s timeout token for the whole pipeline below
/// (model binding → load → validate → SaveChanges/transaction → notify), so an early tab close / network drop
/// no longer turns into a 499 with nothing saved. The original token is restored afterwards so
/// RequestLoggingMiddleware still marks the line " aborted".
/// </summary>
public sealed class DetachWriteCancellationMiddleware(RequestDelegate next)
{
    internal static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWrite(context.Request.Method))
        {
            await next(context);
            return;
        }

        var original = context.RequestAborted;
        using var cts = new CancellationTokenSource(WriteTimeout);
        context.RequestAborted = cts.Token;
        try
        {
            await next(context);
        }
        finally
        {
            context.RequestAborted = original;
        }
    }

    internal static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
