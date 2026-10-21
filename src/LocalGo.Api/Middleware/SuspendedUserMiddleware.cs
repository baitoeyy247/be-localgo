using System.Security.Claims;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Api.Middleware;

/// <summary>Blocks suspended accounts on authenticated API calls (login is checked in auth services).</summary>
public sealed class SuspendedUserMiddleware(RequestDelegate next)
{
    private static readonly PathString[] BypassPrefixes =
    [
        new("/api/health"),
        new("/api/auth/"),
        new("/swagger"),
    ];

    public async Task InvokeAsync(HttpContext context, ILocalGoDbContext db)
    {
        var path = context.Request.Path;
        if (BypassPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            await next(context);
            return;
        }

        var status = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Status)
            .FirstOrDefaultAsync(context.RequestAborted);

        if (status == UserStatus.Suspended)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "บัญชีถูกระงับ",
                detail = "บัญชีนี้ถูกระงับชั่วคราว ติดต่อผู้ดูแลระบบ",
                status = 403,
            });
            return;
        }

        await next(context);
    }
}
