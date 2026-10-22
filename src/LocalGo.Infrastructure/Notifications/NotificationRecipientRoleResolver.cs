using System.Text.Json;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Infrastructure.Notifications;

internal static class NotificationRecipientRoleResolver
{
    public static async Task<string?> ResolveAsync(
        Guid? recipientUserId,
        JsonElement? payload,
        ILocalGoDbContext db,
        CancellationToken cancellationToken)
    {
        if (recipientUserId is null)
        {
            return null;
        }

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == recipientUserId)
            .Select(u => new { u.SystemRole, u.ActiveRole })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (user.SystemRole == SystemRole.Admin)
        {
            return "admin";
        }

        if (Guid.TryParse(GetStr(payload, "requestId"), out var requestId))
        {
            var requesterUserId = await db.ServiceRequests.AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => r.RequesterUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (requesterUserId == recipientUserId)
            {
                return "requester";
            }

            var ownsProvider = await db.Providers.AsNoTracking()
                .AnyAsync(p => p.OwnerUserId == recipientUserId, cancellationToken);

            if (ownsProvider)
            {
                return "provider";
            }
        }

        return user.ActiveRole switch
        {
            AppRole.Requester => "requester",
            AppRole.Provider => "provider",
            _ => null,
        };
    }

    private static string GetStr(JsonElement? el, string key) =>
        el?.TryGetProperty(key, out var v) == true ? v.GetString() ?? "" : "";
}
