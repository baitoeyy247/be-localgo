using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class AdminAppService(ILocalGoDbContext db)
{
    public const string TestUserLinePrefix = "dev-persona-";

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

    public async Task<AdminUsersPageDto> ListUsersAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.DisplayName,
                u.LineUserId,
                u.SystemRole.ToString(),
                u.Status.ToString(),
                u.LineUserId.StartsWith(TestUserLinePrefix)))
            .ToListAsync(cancellationToken);

        return new AdminUsersPageDto(rows.DistinctBy(u => u.Id).ToList(), total, page, pageSize);
    }

    /// <summary>Legacy flat list used by older clients.</summary>
    public async Task<IReadOnlyList<AdminUserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var page = await ListUsersAsync(1, 100, cancellationToken);
        return page.Items;
    }

    public async Task SetUserStatusAsync(Guid userId, string status, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<UserStatus>(status, true, out var parsed))
        {
            throw new AppException("สถานะผู้ใช้ไม่ถูกต้อง", 400);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AppException("ไม่พบผู้ใช้", 404);

        user.Status = parsed;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ResetTestDataAsync(CancellationToken cancellationToken)
    {
        var testUsers = await db.Users
            .Where(u => u.LineUserId.StartsWith(TestUserLinePrefix))
            .ToListAsync(cancellationToken);

        if (testUsers.Count == 0)
        {
            return 0;
        }

        var userIds = testUsers.Select(u => u.Id).ToHashSet();
        var providerIds = await db.Providers
            .Where(p => userIds.Contains(p.OwnerUserId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var requestIds = await db.ServiceRequests
            .Where(r => userIds.Contains(r.RequesterUserId))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var appointmentIds = await db.Appointments
            .Where(a => userIds.Contains(a.RequesterUserId) || providerIds.Contains(a.ProviderId))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var bidIds = await db.Bids
            .Where(b => providerIds.Contains(b.ProviderId) || requestIds.Contains(b.ServiceRequestId))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        db.NotificationLogs.RemoveRange(
            await db.NotificationLogs.Where(n => n.UserId != null && userIds.Contains(n.UserId.Value))
                .ToListAsync(cancellationToken));
        db.Reviews.RemoveRange(
            await db.Reviews.Where(r => userIds.Contains(r.ReviewerUserId) || providerIds.Contains(r.ProviderId))
                .ToListAsync(cancellationToken));
        db.Appointments.RemoveRange(
            await db.Appointments.Where(a => appointmentIds.Contains(a.Id)).ToListAsync(cancellationToken));
        db.Bids.RemoveRange(
            await db.Bids.Where(b => bidIds.Contains(b.Id)).ToListAsync(cancellationToken));
        db.ServiceRequests.RemoveRange(
            await db.ServiceRequests.Where(r => requestIds.Contains(r.Id)).ToListAsync(cancellationToken));

        var services = await db.ProviderServices.Where(s => providerIds.Contains(s.ProviderId)).ToListAsync(cancellationToken);
        var serviceIds = services.Select(s => s.Id).ToList();
        db.ProviderServiceBranches.RemoveRange(
            await db.ProviderServiceBranches.Where(l => serviceIds.Contains(l.ProviderServiceId)).ToListAsync(cancellationToken));
        db.ProviderServices.RemoveRange(services);
        db.ProviderBranches.RemoveRange(
            await db.ProviderBranches.Where(b => providerIds.Contains(b.ProviderId)).ToListAsync(cancellationToken));
        db.Providers.RemoveRange(
            await db.Providers.Where(p => providerIds.Contains(p.Id)).ToListAsync(cancellationToken));
        db.Users.RemoveRange(testUsers);

        await db.SaveChangesAsync(cancellationToken);
        return testUsers.Count;
    }
}
