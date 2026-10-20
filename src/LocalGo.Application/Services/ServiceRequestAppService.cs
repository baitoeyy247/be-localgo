using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalGo.Application.Services;

public sealed class ServiceRequestAppService(
    ILocalGoDbContext db,
    INotificationPublisher notifications,
    ILogger<ServiceRequestAppService> logger,
    IBackgroundWork? background = null)
{
    /// <summary>Title used by scripts/smoke-api.sh — excluded from dev matching lists when requested.</summary>
    public const string SmokeTestRequestTitle = "Smoke test request";

    private static readonly ServiceRequestStatus[] OpenMatchingStatuses =
        [ServiceRequestStatus.Open, ServiceRequestStatus.HasOffers];

    /// <summary>งานที่ผู้ให้บริการถูกเลือกแล้วและยังดำเนินอยู่ — ต้องยังเห็นในรายการแม้ไม่อยู่ใน Open/HasOffers</summary>
    private static readonly ServiceRequestStatus[] ProviderActiveEngagementStatuses =
    [
        ServiceRequestStatus.ProviderSelected,
        ServiceRequestStatus.Scheduled,
        ServiceRequestStatus.InProgress,
    ];

    /// <summary>Creates a draft or published marketplace service request.</summary>
    public async Task<ServiceRequest> CreateAsync(Guid userId, CreateServiceRequestDto request, CancellationToken cancellationToken)
    {
        FieldRules.RequireBudget(request.BudgetText);
        FieldRules.RequireRequestText(request.Title, request.Description);
        var now = DateTime.UtcNow;
        var preferred = FieldRules.RequirePreferredTime(request.PreferredStartAt, request.PreferredEndAt, now);
        var entity = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = userId,
            CategoryId = request.CategoryId,
            Title = request.Title.Trim(),
            Description = request.Description,
            AddressText = request.AddressText,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Location = GeoHelper.CreatePoint(request.Latitude, request.Longitude),
            SearchRadiusMeters = request.SearchRadiusMeters,
            PreferredStartAt = preferred.Start,
            PreferredEndAt = preferred.End,
            BudgetText = request.BudgetText,
            Status = request.Publish ? ServiceRequestStatus.Open : ServiceRequestStatus.Draft,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.ServiceRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        if (request.Publish)
        {
            await QueueMatchingNotificationsAsync(entity.Id, cancellationToken);
        }

        return entity;
    }

    /// <summary>Publishes a draft request and notifies matching providers.</summary>
    public async Task<ServiceRequest> PublishAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await MarketplaceAccess.RequireOwnedRequestAsync(db, userId, requestId, cancellationToken);
        if (entity.Status != ServiceRequestStatus.Draft)
        {
            throw new AppException("Only draft requests can be published.", 409);
        }

        var preferred = FieldRules.RequirePreferredTime(entity.PreferredStartAt, entity.PreferredEndAt, DateTime.UtcNow);
        entity.PreferredStartAt = preferred.Start;
        entity.PreferredEndAt = preferred.End;
        entity.Status = ServiceRequestStatus.Open;
        ProgressHelper.Touch(entity);
        await db.SaveChangesAsync(cancellationToken);
        await QueueMatchingNotificationsAsync(entity.Id, cancellationToken);
        return entity;
    }

    /// <summary>Notifies matching providers for a request that was already saved.</summary>
    public async Task NotifyMatchingProvidersByIdAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ServiceRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return;
        }

        await NotifyMatchingProvidersAsync(request, cancellationToken);
    }

    private async Task QueueMatchingNotificationsAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (background is null)
        {
            await NotifyMatchingProvidersByIdAsync(requestId, cancellationToken);
            return;
        }

        background.Enqueue(async (sp, ct) =>
        {
            var again = sp.GetRequiredService<ServiceRequestAppService>();
            await again.NotifyMatchingProvidersByIdAsync(requestId, ct);
        });
    }

    /// <summary>Lists requests created by the current user.</summary>
    public async Task<IReadOnlyList<ServiceRequestSummaryDto>> ListMineAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var rows = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Title,
                r.Status,
                r.Source,
                CategoryName = r.Category.Name,
                r.CreatedAt,
                BidCount = r.Bids.Count(b => b.Status != BidStatus.Withdrawn && b.Status != BidStatus.Rejected),
                r.BudgetText,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ServiceRequestSummaryDto(
                r.Id,
                r.Title,
                DisplayStatus(r.Status, r.Source, r.BidCount),
                r.CategoryName,
                r.CreatedAt,
                r.BidCount,
                r.BudgetText))
            .ToList();
    }

    /// <summary>Returns request detail when the viewer is allowed to see it; otherwise null.</summary>
    public async Task<ServiceRequest?> GetDetailAsync(
        Guid viewerUserId, Guid requestId, bool isAdmin, CancellationToken cancellationToken)
    {
        var request = await db.ServiceRequests
            .Include(r => r.Bids).ThenInclude(b => b.Provider)
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return null;
        }

        if (!isAdmin && request.RequesterUserId != viewerUserId)
        {
            var provider = await db.Providers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.OwnerUserId == viewerUserId, cancellationToken);
            if (provider is null)
            {
                return null;
            }

            if (!CanProviderViewDetail(request, provider.Id)
                && !await HasActiveAppointmentForProviderAsync(request.Id, provider.Id, cancellationToken))
            {
                return null;
            }

            request.Bids = request.Bids.Where(b => b.ProviderId == provider.Id).ToList();
        }

        return request;
    }

    /// <summary>
    /// Decides whether this user may open the bid form for a LINE deep link.
    /// Does not return the request when access is denied.
    /// </summary>
    public async Task<BidFormAccess> GetBidFormAsync(
        Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ServiceRequests.AsNoTracking()
            .Include(r => r.Bids).ThenInclude(b => b.Provider)
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return new BidFormAccess(false, "ไม่พบงานนี้", null);
        }

        if (request.RequesterUserId == userId)
        {
            return new BidFormAccess(false, "นี่คืองานที่คุณโพสต์เอง จึงเสนอราคาไม่ได้", null);
        }

        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OwnerUserId == userId, cancellationToken);
        if (provider is null)
        {
            return new BidFormAccess(false, "สร้างโปรไฟล์ผู้ให้บริการก่อนจึงจะเสนอราคาได้", null);
        }

        if (provider.Status != ProviderStatus.Active)
        {
            return new BidFormAccess(false, "เปิดใช้งานโปรไฟล์ผู้ให้บริการก่อนจึงจะเสนอราคาได้", null);
        }

        var myBids = request.Bids
            .Where(b => b.ProviderId == provider.Id && b.Status != BidStatus.Withdrawn)
            .ToList();
        var isSelected = request.SelectedProviderId == provider.Id;
        var isOpen = request.Status is ServiceRequestStatus.Open or ServiceRequestStatus.HasOffers;
        var canBid = request.Source == ServiceRequestSource.Marketplace && isOpen;

        if (!canBid && myBids.Count == 0 && !isSelected)
        {
            return new BidFormAccess(false, "งานนี้ไม่เปิดรับข้อเสนอแล้ว", null);
        }

        if (canBid && myBids.Count == 0 && !isSelected)
        {
            var coverage = await LoadCoverageLinksAsync(provider.Id, cancellationToken);
            if (NearestCoverageDistance(coverage, request) is null)
            {
                var (code, message) = await ExplainCoverageDenialAsync(provider.Id, request, cancellationToken);
                return new BidFormAccess(false, message, null, code);
            }
        }
        else if (!canBid)
        {
            return new BidFormAccess(false, "งานนี้ไม่เปิดรับข้อเสนอแล้ว", null);
        }

        request.Bids = myBids;
        return new BidFormAccess(true, null, request);
    }

    /// <summary>Lists open and active jobs matching the provider's service coverage.</summary>
    public async Task<ProviderMatchingInboxDto> ListMatchingForProviderAsync(
        Guid userId,
        bool excludeSmokeTestData,
        bool includeOwnRequests,
        CancellationToken cancellationToken)
    {
        var excludesOwnRequests = !includeOwnRequests;
        var provider = await db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(p => p.OwnerUserId == userId && p.Status == ProviderStatus.Active, cancellationToken);

        if (provider is null)
        {
            return new ProviderMatchingInboxDto([], excludesOwnRequests, []);
        }

        var coverageLinks = await LoadCoverageLinksAsync(provider.Id, cancellationToken);
        var results = new List<ServiceRequestSummaryDto>();
        var seenIds = new HashSet<Guid>();

        if (coverageLinks.Count > 0)
        {
            await AppendOpenMatchingRequestsAsync(
                provider,
                userId,
                coverageLinks,
                excludeSmokeTestData,
                includeOwnRequests,
                results,
                seenIds,
                cancellationToken);
        }

        await AppendActiveEngagementsAsync(
            provider,
            userId,
            coverageLinks,
            excludeSmokeTestData,
            includeOwnRequests,
            results,
            seenIds,
            cancellationToken);

        var items = results
            .OrderByDescending(r => ProviderActiveEngagementStatuses.Any(s => s.ToString() == r.Status))
            .ThenByDescending(r => r.CreatedAt)
            .ThenBy(r => r.DistanceMeters)
            .ToList();

        return new ProviderMatchingInboxDto(items, excludesOwnRequests, DistinctAreas(coverageLinks));
    }

    /// <summary>Cancels a request owned by the current user and tells bidders.</summary>
    public async Task CancelAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await MarketplaceAccess.RequireOwnedRequestAsync(db, userId, requestId, cancellationToken);
        var providerIds = await db.Bids.AsNoTracking()
            .Where(b => b.ServiceRequestId == requestId && b.Status != BidStatus.Withdrawn)
            .Select(b => b.ProviderId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var owners = providerIds.Count == 0
            ? []
            : await db.Providers.AsNoTracking()
                .Where(p => providerIds.Contains(p.Id))
                .Join(
                    db.Users.AsNoTracking(),
                    p => p.OwnerUserId,
                    u => u.Id,
                    (_, u) => new { u.Id, u.LineUserId })
                .ToListAsync(cancellationToken);

        entity.Status = ServiceRequestStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (owners.Count == 0)
        {
            return;
        }

        var categoryName = await db.ServiceCategories.AsNoTracking()
            .Where(c => c.Id == entity.CategoryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";
        var notified = new HashSet<Guid>();
        foreach (var owner in owners)
        {
            if (!notified.Add(owner.Id))
            {
                continue;
            }

            await NotificationEnqueue.TryExcludingActorAsync(
                logger,
                notifications,
                NotificationEvents.RequestCancelled,
                owner.Id,
                userId,
                owner.LineUserId,
                new
                {
                    requestId = entity.Id,
                    title = entity.Title,
                    categoryId = entity.CategoryId,
                    categoryName,
                },
                cancellationToken,
                "Request cancelled but notification enqueue failed. RequestId={RequestId} RecipientId={RecipientId}",
                entity.Id,
                owner.Id);
        }
    }

    /// <summary>Marks a confirmed appointment's request as in progress. Provider only when an appointment exists.</summary>
    public async Task StartAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);
        await MarketplaceAccess.EnsureServiceRequestParticipantAsync(db, userId, entity, cancellationToken);
        await MoveActiveAppointmentAsync(
            userId,
            entity.Id,
            AppointmentStatus.Confirmed,
            AppointmentStatus.InProgress,
            "ยืนยันนัดหมายก่อนเริ่มงาน",
            cancellationToken);
        entity.Status = ServiceRequestStatus.InProgress;
        ProgressHelper.Touch(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Marks an in-progress job completed. Provider only when an appointment exists.</summary>
    public async Task CompleteAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new AppException("Request not found.", 404);
        await MarketplaceAccess.EnsureServiceRequestParticipantAsync(db, userId, entity, cancellationToken);
        await MoveActiveAppointmentAsync(
            userId,
            entity.Id,
            AppointmentStatus.InProgress,
            AppointmentStatus.Completed,
            "เริ่มงานก่อนปิดงาน",
            cancellationToken);
        entity.Status = ServiceRequestStatus.Completed;
        ProgressHelper.Touch(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MoveActiveAppointmentAsync(
        Guid userId,
        Guid requestId,
        AppointmentStatus requiredCurrent,
        AppointmentStatus next,
        string blockedMessage,
        CancellationToken cancellationToken)
    {
        var appointment = await db.Appointments
            .FirstOrDefaultAsync(
                a => a.ServiceRequestId == requestId && a.Status != AppointmentStatus.Cancelled,
                cancellationToken);
        if (appointment is null || appointment.Status == next)
        {
            return;
        }

        if (appointment.Status != requiredCurrent)
        {
            throw new AppException(blockedMessage, 409);
        }

        var isProvider = await db.Providers.AsNoTracking().AnyAsync(
            p => p.OwnerUserId == userId && p.Id == appointment.ProviderId,
            cancellationToken);
        if (!isProvider)
        {
            throw new AppException("เฉพาะผู้ให้บริการที่เดินงานนี้ต่อได้", 403);
        }

        appointment.Status = next;
        appointment.UpdatedAt = DateTime.UtcNow;
    }

    public async Task ExpireStaleAsync(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddDays(-14);
        var stale = await db.ServiceRequests
            .Where(r =>
                (r.Status == ServiceRequestStatus.HasOffers || r.Status == ServiceRequestStatus.ProviderSelected)
                && r.FirstBidAt != null
                && r.LastProgressAt <= threshold)
            .ToListAsync(cancellationToken);

        foreach (var request in stale)
        {
            request.Status = ServiceRequestStatus.Expired;
            request.ExpiresAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            var owner = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId, cancellationToken);
            if (owner is not null)
            {
                var categoryName = await db.ServiceCategories.AsNoTracking()
                    .Where(c => c.Id == request.CategoryId)
                    .Select(c => c.Name)
                    .FirstAsync(cancellationToken);

                await notifications.EnqueueAsync(
                    NotificationEvents.RequestExpired,
                    owner.Id,
                    owner.LineUserId,
                    new { requestId = request.Id, title = request.Title, categoryId = request.CategoryId, categoryName },
                    cancellationToken);
            }
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Sends a one-time "expiring soon" notification to requesters whose requests have bids
    /// but no progress for 12–13 days (still 1–2 days before the 14-day expiry).
    /// Deduplicates by checking for an existing Sent/Pending log for the same request.
    /// </summary>
    public async Task NotifyExpiringSoonAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(-12);
        var windowStart = now.AddDays(-13);

        var atRisk = await db.ServiceRequests
            .Where(r =>
                (r.Status == ServiceRequestStatus.HasOffers || r.Status == ServiceRequestStatus.ProviderSelected)
                && r.FirstBidAt != null
                && r.LastProgressAt <= windowEnd
                && r.LastProgressAt >= windowStart)
            .ToListAsync(cancellationToken);

        foreach (var request in atRisk)
        {
            var requestIdStr = request.Id.ToString();
            var alreadyNotified = await db.NotificationLogs.AnyAsync(
                n => n.EventType == NotificationEvents.RequestExpiringSoon
                     && n.UserId == request.RequesterUserId
                     && n.PayloadJson != null
                     && n.PayloadJson.Contains(requestIdStr)
                     && (n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Pending),
                cancellationToken);

            if (alreadyNotified)
            {
                continue;
            }

            var owner = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.RequesterUserId, cancellationToken);
            if (owner is null)
            {
                continue;
            }

            var daysRemaining = 14 - (int)(now - request.LastProgressAt!.Value).TotalDays;
            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == request.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            await notifications.EnqueueAsync(
                NotificationEvents.RequestExpiringSoon,
                owner.Id,
                owner.LineUserId,
                new { requestId = request.Id, title = request.Title, categoryId = request.CategoryId, categoryName, daysRemaining },
                cancellationToken);
        }
    }

    /// <summary>Selects a provider directly (e.g. from search) without a marketplace bid.</summary>
    public async Task SelectProviderAsync(Guid userId, Guid requestId, Guid providerId, CancellationToken cancellationToken)
    {
        var serviceRequest = await MarketplaceAccess.RequireOwnedRequestAsync(db, userId, requestId, cancellationToken);

        if (serviceRequest.Status is not (
            ServiceRequestStatus.Open
            or ServiceRequestStatus.HasOffers
            or ServiceRequestStatus.Draft))
        {
            throw new AppException("Cannot select provider for this request status.", 409);
        }

        var provider = await db.Providers.AsNoTracking().Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == providerId && p.Status == ProviderStatus.Active, cancellationToken)
            ?? throw new AppException("Provider not found.", 404);

        if (serviceRequest.SelectedProviderId == providerId
            && serviceRequest.Status == ServiceRequestStatus.ProviderSelected)
        {
            return;
        }

        serviceRequest.SelectedProviderId = providerId;
        serviceRequest.SelectedBidId = null;
        serviceRequest.Status = ServiceRequestStatus.ProviderSelected;
        ProgressHelper.Touch(serviceRequest);
        await db.SaveChangesAsync(cancellationToken);

        if (provider.OwnerUser is not null)
        {
            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == serviceRequest.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            await NotificationEnqueue.TryExcludingActorAsync(
                logger,
                notifications,
                NotificationEvents.BidSelected,
                provider.OwnerUser.Id,
                userId,
                provider.OwnerUser.LineUserId,
                new
                {
                    requestId,
                    title = serviceRequest.Title,
                    categoryId = serviceRequest.CategoryId,
                    categoryName,
                    directFromSearch = true,
                },
                cancellationToken,
                "Provider selected but notification enqueue failed. RequestId={RequestId} ProviderId={ProviderId}",
                requestId,
                providerId);
        }
    }

    private static bool CanProviderViewDetail(ServiceRequest request, Guid providerId) =>
        request.SelectedProviderId == providerId
        || OpenMatchingStatuses.Contains(request.Status)
        || request.Bids.Any(b => b.ProviderId == providerId);

    private async Task<bool> HasActiveAppointmentForProviderAsync(
        Guid requestId, Guid providerId, CancellationToken cancellationToken) =>
        await db.Appointments.AsNoTracking().AnyAsync(
            a => a.ServiceRequestId == requestId
                && a.ProviderId == providerId
                && a.Status != AppointmentStatus.Cancelled,
            cancellationToken);

    private async Task<List<CoverageLink>> LoadCoverageLinksAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var activeServices = await db.ProviderServices.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .Select(s => new { s.Id, s.CategoryId })
            .ToListAsync(cancellationToken);

        var activeBranches = await db.ProviderBranches.AsNoTracking()
            .Where(b => b.ProviderId == providerId && b.IsActive)
            .ToListAsync(cancellationToken);

        var activeLinks = await db.ProviderServiceBranches.AsNoTracking()
            .Where(l => l.IsActive)
            .Join(
                db.ProviderServices.AsNoTracking().Where(s => s.ProviderId == providerId && s.IsActive),
                l => l.ProviderServiceId,
                s => s.Id,
                (l, s) => new { ServiceId = s.Id, l.ProviderBranchId })
            .ToListAsync(cancellationToken);

        // Older data may lack service-branch links; treat all active branches as coverage for that service.
        return activeServices
            .SelectMany(service =>
            {
                var linkedBranchIds = activeLinks
                    .Where(l => l.ServiceId == service.Id)
                    .Select(l => l.ProviderBranchId)
                    .ToHashSet();

                var branches = linkedBranchIds.Count > 0
                    ? activeBranches.Where(b => linkedBranchIds.Contains(b.Id))
                    : activeBranches;

                return branches.Select(branch => new CoverageLink(service.CategoryId, branch));
            })
            .ToList();
    }

    private async Task AppendOpenMatchingRequestsAsync(
        Provider provider,
        Guid userId,
        IReadOnlyList<CoverageLink> coverageLinks,
        bool excludeSmokeTestData,
        bool includeOwnRequests,
        List<ServiceRequestSummaryDto> results,
        HashSet<Guid> seenIds,
        CancellationToken cancellationToken)
    {
        var categoryIds = coverageLinks.Select(c => c.CategoryId).Distinct().ToArray();
        var openQuery = db.ServiceRequests.AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Bids)
            .Where(r =>
                r.Source == ServiceRequestSource.Marketplace
                && OpenMatchingStatuses.Contains(r.Status)
                && categoryIds.Contains(r.CategoryId)
                && (includeOwnRequests || r.RequesterUserId != userId));

        if (excludeSmokeTestData)
        {
            openQuery = openQuery.Where(r => r.Title != SmokeTestRequestTitle);
        }

        var openRequests = await openQuery.ToListAsync(cancellationToken);

        foreach (var request in openRequests)
        {
            var distance = NearestCoverageDistance(coverageLinks, request);
            if (distance is null)
            {
                continue;
            }

            seenIds.Add(request.Id);
            results.Add(ToSummary(request, provider.Id, userId, distance));
        }
    }

    private async Task AppendActiveEngagementsAsync(
        Provider provider,
        Guid userId,
        IReadOnlyList<CoverageLink> coverageLinks,
        bool excludeSmokeTestData,
        bool includeOwnRequests,
        List<ServiceRequestSummaryDto> results,
        HashSet<Guid> seenIds,
        CancellationToken cancellationToken)
    {
        var activeQuery = db.ServiceRequests.AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Bids)
            .Where(r =>
                ProviderActiveEngagementStatuses.Contains(r.Status)
                && (includeOwnRequests || r.RequesterUserId != userId)
                && (
                    (r.Source == ServiceRequestSource.Marketplace
                        && (r.SelectedProviderId == provider.Id
                            || r.Bids.Any(b => b.ProviderId == provider.Id && b.Status == BidStatus.Selected)))
                    || (r.Source == ServiceRequestSource.DirectBooking && r.SelectedProviderId == provider.Id)));

        if (excludeSmokeTestData)
        {
            activeQuery = activeQuery.Where(r => r.Title != SmokeTestRequestTitle);
        }

        var activeEngagements = await activeQuery.ToListAsync(cancellationToken);
        var activeIds = activeEngagements.Select(r => r.Id).ToList();
        var pendingConfirmRequestIds = activeIds.Count == 0
            ? new HashSet<Guid>()
            : await db.Appointments.AsNoTracking()
                .Where(a =>
                    activeIds.Contains(a.ServiceRequestId)
                    && a.ProviderId == provider.Id
                    && a.Status == AppointmentStatus.Pending)
                .Select(a => a.ServiceRequestId)
                .ToHashSetAsync(cancellationToken);

        foreach (var request in activeEngagements)
        {
            if (!seenIds.Add(request.Id))
            {
                continue;
            }

            double? distance = null;
            if (coverageLinks.Count > 0)
            {
                distance = NearestCoverageDistance(coverageLinks, request);
                if (distance is null)
                {
                    continue;
                }
            }

            results.Add(ToSummary(
                request,
                provider.Id,
                userId,
                distance,
                pendingConfirmRequestIds.Contains(request.Id)));
        }
    }

    private static double? NearestCoverageDistance(IReadOnlyList<CoverageLink> coverageLinks, ServiceRequest request)
    {
        var nearest = coverageLinks
            .Where(c => c.CategoryId == request.CategoryId)
            .Select(c => new
            {
                c.Branch,
                Distance = GeoHelper.DistanceMeters(c.Branch.Location, request.Location),
            })
            .Where(x => x.Distance <= request.SearchRadiusMeters && x.Distance <= x.Branch.ServiceRadiusMeters)
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return nearest?.Distance;
    }

    private static ServiceRequestSummaryDto ToSummary(
        ServiceRequest request,
        Guid providerId,
        Guid viewerUserId,
        double? distanceMeters,
        bool needsAppointmentConfirm = false)
    {
        var alreadyBid = request.Bids.Any(b =>
            b.ProviderId == providerId
            && (b.Status == BidStatus.Submitted || b.Status == BidStatus.Selected));

        return new ServiceRequestSummaryDto(
            request.Id,
            request.Title,
            request.Status.ToString(),
            request.Category.Name,
            request.CreatedAt,
            request.Bids.Count(b => b.Status != BidStatus.Withdrawn),
            request.BudgetText,
            distanceMeters,
            alreadyBid,
            needsAppointmentConfirm,
            request.RequesterUserId == viewerUserId);
    }

    private static string DisplayStatus(
        ServiceRequestStatus status,
        ServiceRequestSource source,
        int activeBidCount)
    {
        if (source == ServiceRequestSource.Marketplace
            && activeBidCount == 0
            && status is ServiceRequestStatus.HasOffers
                or ServiceRequestStatus.ProviderSelected
                or ServiceRequestStatus.Scheduled)
        {
            return ServiceRequestStatus.Open.ToString();
        }

        return status.ToString();
    }

    private async Task<(string Code, string Message)> ExplainCoverageDenialAsync(
        Guid providerId,
        ServiceRequest request,
        CancellationToken cancellationToken)
    {
        var services = await db.ProviderServices.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.CategoryId == request.CategoryId)
            .Select(s => s.IsActive)
            .ToListAsync(cancellationToken);
        if (services.Count == 0)
        {
            return ("CATEGORY_MISMATCH", "งานนี้ไม่ตรงกับหมวดบริการที่คุณเปิดอยู่");
        }

        if (services.All(active => !active))
        {
            return ("SERVICE_PAUSED", "บริการในหมวดนี้อยู่ในสถานะหยุดชั่วคราว");
        }

        var branches = await db.ProviderBranches.AsNoTracking()
            .Where(b => b.ProviderId == providerId)
            .Select(b => b.IsActive)
            .ToListAsync(cancellationToken);
        if (branches.Count == 0 || branches.All(active => !active))
        {
            return ("AREA_DISABLED", "พื้นที่ให้บริการถูกปิดใช้งาน");
        }

        return ("OUT_OF_RANGE", "งานนี้อยู่นอกพื้นที่ให้บริการของคุณ");
    }

    private async Task NotifyMatchingProvidersAsync(ServiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Source != ServiceRequestSource.Marketplace
            || !OpenMatchingStatuses.Contains(request.Status))
        {
            return;
        }

        var candidateProviderIds = await db.ProviderServices.AsNoTracking()
            .Where(s => s.IsActive && s.CategoryId == request.CategoryId)
            .Join(
                db.Providers.AsNoTracking().Where(p =>
                    p.Status == ProviderStatus.Active && p.OwnerUserId != request.RequesterUserId),
                s => s.ProviderId,
                p => p.Id,
                (_, p) => p.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var notifiedOwners = new HashSet<Guid>();
        foreach (var providerId in candidateProviderIds)
        {
            var coverageLinks = await LoadCoverageLinksAsync(providerId, cancellationToken);
            if (coverageLinks.Count == 0 || NearestCoverageDistance(coverageLinks, request) is null)
            {
                continue;
            }

            var ownerUserId = await db.Providers.AsNoTracking()
                .Where(p => p.Id == providerId)
                .Select(p => p.OwnerUserId)
                .FirstAsync(cancellationToken);

            if (!notifiedOwners.Add(ownerUserId))
            {
                continue;
            }

            var owner = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == ownerUserId, cancellationToken);
            if (owner is null)
            {
                continue;
            }

            var categoryName = await db.ServiceCategories.AsNoTracking()
                .Where(c => c.Id == request.CategoryId)
                .Select(c => c.Name)
                .FirstAsync(cancellationToken);

            await NotificationEnqueue.TryExcludingActorAsync(
                logger,
                notifications,
                NotificationEvents.NewMatchingRequest,
                owner.Id,
                request.RequesterUserId,
                owner.LineUserId,
                new { requestId = request.Id, title = request.Title, categoryId = request.CategoryId, categoryName },
                cancellationToken,
                "New matching request notification enqueue failed. RequestId={RequestId} ProviderId={ProviderId}",
                request.Id,
                providerId);
        }
    }

    private static List<ProviderCoverageAreaDto> DistinctAreas(IReadOnlyList<CoverageLink> coverageLinks) =>
        coverageLinks
            .GroupBy(link => link.Branch.Id)
            .Select(group =>
            {
                var branch = group.First().Branch;
                return new ProviderCoverageAreaDto(branch.Name, branch.ServiceRadiusMeters);
            })
            .OrderBy(area => area.Name)
            .ToList();

    private sealed record CoverageLink(Guid CategoryId, ProviderBranch Branch);
}

public sealed record BidFormAccess(bool Allowed, string? Reason, ServiceRequest? Request, string? ReasonCode = null);
