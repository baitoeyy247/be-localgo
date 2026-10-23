using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Common;

/// <summary>
/// R46 D1/D2: single recipient gate applied by NotificationPublisher before ANY sender (console or LINE).
/// Only Active users receive notifications; AccountSuspended is the one notice a suspended user still gets.
/// </summary>
public static class NotificationRecipientFilter
{
    public const string SuspendedReason = "Recipient suspended";
    public const string DeletedReason = "Recipient deleted";

    public static string? BlockReason(UserStatus? status, string eventType)
    {
        if (status is null || status == UserStatus.Active) return null;
        if (status == UserStatus.Suspended && eventType == NotificationEvents.AccountSuspended) return null;
        return status == UserStatus.Deleted ? DeletedReason : SuspendedReason;
    }

    public static async Task<string?> BlockReasonAsync(
        ILocalGoDbContext db, Guid? userId, string eventType, CancellationToken cancellationToken)
    {
        if (userId is not { } id) return null;
        var status = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => (UserStatus?)u.Status)
            .FirstOrDefaultAsync(cancellationToken);
        return BlockReason(status, eventType);
    }

    /// <summary>True when the user may receive non-account notifications (matching loops etc.).</summary>
    public static bool IsReachable(UserStatus status) => status == UserStatus.Active;
}
