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
}
