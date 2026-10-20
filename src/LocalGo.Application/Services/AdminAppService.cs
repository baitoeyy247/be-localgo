using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class AdminAppService(ILocalGoDbContext db)
{
    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        return new AdminDashboardDto(
            await db.Users.CountAsync(cancellationToken),
            await db.Providers.CountAsync(cancellationToken),
            await db.ServiceRequests.CountAsync(r => r.Status != ServiceRequestStatus.Draft, cancellationToken),
            await db.ServiceRequests.CountAsync(r => r.Status == ServiceRequestStatus.Completed, cancellationToken),
            await db.ServiceRequests.CountAsync(r => r.Status == ServiceRequestStatus.Expired, cancellationToken),
            await db.NotificationLogs.CountAsync(n => n.Status == NotificationStatus.Pending, cancellationToken));
    }

    /// <summary>Projects one row per user. Scalar columns avoid navigation joins that can repeat a person.</summary>
    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto(
                u.Id,
                u.DisplayName,
                u.LineUserId,
                u.SystemRole.ToString()))
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.DistinctBy(u => u.Id).ToList();
    }
}
