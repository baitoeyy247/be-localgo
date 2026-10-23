using LocalGo.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace LocalGo.Tests;

public class DetachWriteCancellationTests
{
    [Fact]
    public async Task Patch_cancelled_before_load_still_loads_saves_and_notifies()
    {
        var clientAbort = new CancellationTokenSource();
        clientAbort.Cancel(); // client gone before the handler even loads the appointment
        var ctx = new DefaultHttpContext { RequestAborted = clientAbort.Token };
        ctx.Request.Method = "PATCH";
        ctx.Request.Path = "/api/appointments/abc";
        var steps = new List<string>();

        var mw = new DetachWriteCancellationMiddleware(async c =>
        {
            var ct = c.RequestAborted; // what [ApiController] binds as CancellationToken
            await Task.Delay(10, ct); steps.Add("load");
            await Task.Delay(10, ct); steps.Add("save");
            await Task.Delay(10, ct); steps.Add("notify");
            c.Response.StatusCode = 200;
        });

        await mw.InvokeAsync(ctx);

        Assert.Equal(["load", "save", "notify"], steps);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.True(ctx.RequestAborted.IsCancellationRequested); // original restored → [REQ] still says aborted
    }

    [Fact]
    public async Task Get_keeps_client_token()
    {
        var clientAbort = new CancellationTokenSource();
        clientAbort.Cancel();
        var ctx = new DefaultHttpContext { RequestAborted = clientAbort.Token };
        ctx.Request.Method = "GET";
        var seen = false;
        await new DetachWriteCancellationMiddleware(c => { seen = c.RequestAborted.IsCancellationRequested; return Task.CompletedTask; })
            .InvokeAsync(ctx);
        Assert.True(seen);
    }
}
