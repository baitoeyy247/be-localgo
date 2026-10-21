using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class ReviewAppService(ILocalGoDbContext db, INotificationPublisher notifications)
{
    public async Task<ReviewCreatedDto> CreateAsync(Guid userId, Guid providerId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReviewType>(request.ReviewType, true, out var reviewType))
        {
            throw new AppException("Invalid review type.", 400);
        }

        if (request.Rating is < 1 or > 5)
        {
            throw new AppException("Rating must be 1-5.", 400);
        }

        FieldRules.RequireReviewComment(request.Comment);

        // Verified reviews always attach to the provider who actually did the job
        // (appointment / selected provider) — never trust a stale client cache id.
        var effectiveProviderId = providerId;
        if (reviewType == ReviewType.Verified)
        {
            if (request.ServiceRequestId is null)
            {
                throw new AppException("serviceRequestId required for verified review.", 400);
            }

            var sr = await db.ServiceRequests.AsNoTracking().FirstOrDefaultAsync(
                r => r.Id == request.ServiceRequestId && r.RequesterUserId == userId, cancellationToken);
            if (sr is null || sr.Status != ServiceRequestStatus.Completed)
            {
                throw new AppException("Verified review requires completed request.", 409);
            }

            effectiveProviderId = await ResolveJobProviderIdAsync(sr.Id, sr.SelectedProviderId, cancellationToken)
                ?? throw new AppException("ไม่พบผู้ให้บริการของงานนี้", 409);
        }

        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Id == effectiveProviderId, cancellationToken)
            ?? throw new AppException("Provider not found.", 404);

        if (request.ServiceRequestId is Guid serviceRequestId
            && await db.Reviews.AnyAsync(
                r => r.ServiceRequestId == serviceRequestId && r.ReviewerUserId == userId,
                cancellationToken))
        {
            throw new AppException("คุณรีวิวงานนี้แล้ว", 409);
        }

        var reviewerName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "";

        var now = DateTime.UtcNow;
        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerUserId = userId,
            ProviderId = effectiveProviderId,
            ServiceRequestId = request.ServiceRequestId,
            ReviewType = reviewType,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ReviewStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Reviews.Add(review);
        await RecalculateProviderRatingsAsync(provider, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (request.ServiceRequestId is not null)
        {
            throw new AppException("คุณรีวิวงานนี้แล้ว", 409);
        }

        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == provider.OwnerUserId, cancellationToken);
        if (owner is not null && NotificationRecipients.ShouldNotify(owner.Id, userId))
        {
            await notifications.EnqueueAsync(
                NotificationEvents.ReviewReceived,
                owner.Id,
                owner.LineUserId,
                new
                {
                    providerId = effectiveProviderId,
                    rating = review.Rating,
                    reviewType = review.ReviewType.ToString(),
                    serviceRequestId = review.ServiceRequestId,
                },
                cancellationToken);
        }

        return new ReviewCreatedDto(
            review.Id,
            review.Rating,
            review.Comment,
            review.ReviewType.ToString(),
            review.CreatedAt,
            reviewerName,
            review.ServiceRequestId);
    }

    public async Task<Review?> FindMineAsync(
        Guid userId, Guid providerId, Guid serviceRequestId, CancellationToken cancellationToken) =>
        await db.Reviews.AsNoTracking()
            .Include(r => r.ReviewerUser)
            .FirstOrDefaultAsync(
                r => r.ProviderId == providerId
                    && r.ReviewerUserId == userId
                    && r.ServiceRequestId == serviceRequestId,
                cancellationToken);

    public async Task<IReadOnlyList<Review>> ListForProviderAsync(Guid providerId, CancellationToken cancellationToken) =>
        await db.Reviews.AsNoTracking()
            .Include(r => r.ReviewerUser)
            .Where(r => r.ProviderId == providerId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Moves a mis-assigned review onto the job's real provider (or QA ผู้ให้ 01) and recalculates ratings.
    /// </summary>
    public async Task<bool> ReassignReviewForRequestAsync(
        Guid serviceRequestId,
        Guid? preferredProviderId,
        CancellationToken cancellationToken)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(
            r => r.ServiceRequestId == serviceRequestId, cancellationToken);
        if (review is null)
        {
            return false;
        }

        var sr = await db.ServiceRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == serviceRequestId, cancellationToken);

        var targetId = preferredProviderId
            ?? await ResolveJobProviderIdAsync(serviceRequestId, sr?.SelectedProviderId, cancellationToken);

        if (targetId is null || targetId == review.ProviderId)
        {
            var current = await db.Providers.FirstOrDefaultAsync(p => p.Id == review.ProviderId, cancellationToken);
            if (current is not null)
            {
                await RecalculateProviderRatingsAsync(current, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            return targetId == review.ProviderId;
        }

        var oldProvider = await db.Providers.FirstOrDefaultAsync(p => p.Id == review.ProviderId, cancellationToken);
        var newProvider = await db.Providers.FirstOrDefaultAsync(p => p.Id == targetId.Value, cancellationToken)
            ?? throw new AppException("Provider not found.", 404);

        review.ProviderId = targetId.Value;
        review.UpdatedAt = DateTime.UtcNow;

        if (oldProvider is not null)
        {
            await RecalculateProviderRatingsAsync(oldProvider, cancellationToken);
        }

        await RecalculateProviderRatingsAsync(newProvider, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecalculateRatingsForProviderAsync(Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return;
        }

        await RecalculateProviderRatingsAsync(provider, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveJobProviderIdAsync(
        Guid serviceRequestId,
        Guid? selectedProviderId,
        CancellationToken cancellationToken)
    {
        var fromAppointment = await db.Appointments.AsNoTracking()
            .Where(a => a.ServiceRequestId == serviceRequestId
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.Rejected)
            .OrderByDescending(a => a.Status == AppointmentStatus.Completed)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(a => (Guid?)a.ProviderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fromAppointment is Guid apptProvider)
        {
            return apptProvider;
        }

        if (selectedProviderId is Guid selected)
        {
            return selected;
        }

        return await db.Appointments.AsNoTracking()
            .Where(a => a.ServiceRequestId == serviceRequestId)
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => (Guid?)a.ProviderId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task RecalculateProviderRatingsAsync(Provider provider, CancellationToken cancellationToken)
    {
        var verified = await db.Reviews.Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Verified && r.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);
        var pub = await db.Reviews.Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Public && r.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);

        // Include the in-memory review being added but not yet saved.
        var pendingVerified = db.Reviews.Local
            .Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Verified && r.Status == ReviewStatus.Published)
            .ToList();
        var pendingPublic = db.Reviews.Local
            .Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Public && r.Status == ReviewStatus.Published)
            .ToList();

        foreach (var r in pendingVerified.Where(r => verified.All(v => v.Id != r.Id)))
        {
            verified.Add(r);
        }

        foreach (var r in pendingPublic.Where(r => pub.All(v => v.Id != r.Id)))
        {
            pub.Add(r);
        }

        provider.VerifiedReviewCount = verified.Count;
        provider.PublicReviewCount = pub.Count;
        provider.AverageVerifiedRating = verified.Count == 0 ? 0 : (decimal)verified.Average(r => r.Rating);
        provider.AveragePublicRating = pub.Count == 0 ? 0 : (decimal)pub.Average(r => r.Rating);
        provider.UpdatedAt = DateTime.UtcNow;
    }
}
