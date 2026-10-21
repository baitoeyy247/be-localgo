using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class AdminAppService(ILocalGoDbContext db, ReviewAppService reviews)
{
    public const string TestUserLinePrefix = "dev-persona-";

    /// <summary>Known SIT request whose verified review was attached to the wrong provider.</summary>
    public static readonly Guid MisassignedReviewRequestId =
        Guid.Parse("bf9b907c-aefa-4bc4-91e5-138958eff2f8");

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

    /// <summary>
    /// Repairs the known mis-assigned review onto QA ผู้ให้ 01 (or the job's appointment provider)
    /// and drops stale Bangkok-default / duplicate-title test marketplace rows.
    /// </summary>
    public async Task<object> RepairSitPlaytestDataAsync(CancellationToken cancellationToken)
    {
        var qaProviderId = await (
            from p in db.Providers.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.OwnerUserId equals u.Id
            where u.LineUserId == $"{TestUserLinePrefix}prov-01" || p.Name.Contains("ผู้ให้ 01")
            orderby u.LineUserId == $"{TestUserLinePrefix}prov-01" ? 0 : 1
            select (Guid?)p.Id
        ).FirstOrDefaultAsync(cancellationToken);

        var reassigned = await reviews.ReassignReviewForRequestAsync(
            MisassignedReviewRequestId, qaProviderId, cancellationToken);

        // Soft-clean leftover test requests that still use Bangkok map defaults or duplicated titles.
        const double bkkLat = 13.7563;
        const double bkkLng = 100.5018;
        var testUserIds = await db.Users.AsNoTracking()
            .Where(u => u.LineUserId.StartsWith(TestUserLinePrefix))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var candidates = await db.ServiceRequests
            .Where(r => testUserIds.Contains(r.RequesterUserId)
                && r.Status != ServiceRequestStatus.Completed
                && r.Status != ServiceRequestStatus.InProgress)
            .ToListAsync(cancellationToken);

        var duplicateTitles = candidates
            .GroupBy(r => (r.RequesterUserId, r.Title))
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.OrderByDescending(x => x.CreatedAt).Skip(1))
            .Select(r => r.Id)
            .ToHashSet();

        var stale = candidates
            .Where(r =>
                duplicateTitles.Contains(r.Id)
                || (Math.Abs(r.Latitude - bkkLat) < 0.0002 && Math.Abs(r.Longitude - bkkLng) < 0.0002))
            .ToList();

        var staleIds = stale.Select(r => r.Id).ToList();

        // Orphan direct-booking jobs: request saved but appointment insert rolled back / failed.
        var orphanDirectBookings = await db.ServiceRequests
            .Where(r => testUserIds.Contains(r.RequesterUserId)
                && r.Source == ServiceRequestSource.DirectBooking
                && !db.Appointments.Any(a => a.ServiceRequestId == r.Id))
            .ToListAsync(cancellationToken);
        foreach (var orphan in orphanDirectBookings)
        {
            if (!staleIds.Contains(orphan.Id))
            {
                stale.Add(orphan);
                staleIds.Add(orphan.Id);
            }
        }

        if (staleIds.Count > 0)
        {
            db.Appointments.RemoveRange(
                await db.Appointments.Where(a => staleIds.Contains(a.ServiceRequestId)).ToListAsync(cancellationToken));
            db.Bids.RemoveRange(
                await db.Bids.Where(b => staleIds.Contains(b.ServiceRequestId)).ToListAsync(cancellationToken));
            db.Reviews.RemoveRange(
                await db.Reviews.Where(r => r.ServiceRequestId != null && staleIds.Contains(r.ServiceRequestId.Value))
                    .ToListAsync(cancellationToken));
            db.ServiceRequests.RemoveRange(stale);
            await db.SaveChangesAsync(cancellationToken);
        }

        var providersTouched = await reviews.RecalculateAllProviderRatingsAsync(cancellationToken);

        return new
        {
            reviewReassigned = reassigned,
            targetProviderId = qaProviderId,
            staleRequestsRemoved = staleIds.Count,
            providersRecalculated = providersTouched,
        };
    }

    /// <summary>
    /// Clears marketplace playtest data for persona accounts but keeps all 10 test users (and their provider profiles).
    /// </summary>
    public async Task<object> ResetTestDataAsync(CancellationToken cancellationToken)
    {
        var testUsers = await db.Users
            .Where(u => u.LineUserId.StartsWith(TestUserLinePrefix))
            .ToListAsync(cancellationToken);

        if (testUsers.Count == 0)
        {
            return new { deletedUsers = 0, keptUsers = 0, deletedRequests = 0, providersRecalculated = 0 };
        }

        var userIds = testUsers.Select(u => u.Id).ToHashSet();
        var providerIds = await db.Providers
            .Where(p => userIds.Contains(p.OwnerUserId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var requestIds = await db.ServiceRequests
            .Where(r => userIds.Contains(r.RequesterUserId)
                || (r.SelectedProviderId != null && providerIds.Contains(r.SelectedProviderId.Value)))
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

        await db.SaveChangesAsync(cancellationToken);

        // Keep all persona users + provider profiles; only refresh aggregates from remaining reviews.
        var providersTouched = await reviews.RecalculateAllProviderRatingsAsync(cancellationToken);

        return new
        {
            deletedUsers = 0,
            keptUsers = testUsers.Count,
            deletedRequests = requestIds.Count,
            providersRecalculated = providersTouched,
        };
    }
}
