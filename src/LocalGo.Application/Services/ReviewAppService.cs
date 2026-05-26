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
    public async Task<Review> CreateAsync(Guid userId, Guid providerId, CreateReviewRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReviewType>(request.ReviewType, true, out var reviewType))
        {
            throw new AppException("Invalid review type.", 400);
        }

        if (request.Rating is < 1 or > 5)
        {
            throw new AppException("Rating must be 1-5.", 400);
        }

        var provider = await db.Providers.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken)
            ?? throw new AppException("Provider not found.", 404);

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
        }

        var now = DateTime.UtcNow;
        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerUserId = userId,
            ProviderId = providerId,
            ServiceRequestId = request.ServiceRequestId,
            ReviewType = reviewType,
            Rating = request.Rating,
            Comment = request.Comment,
            Status = ReviewStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Reviews.Add(review);
        await RecalculateProviderRatingsAsync(provider, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var owner = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == provider.OwnerUserId, cancellationToken);
        if (owner is not null && NotificationRecipients.ShouldNotify(owner.Id, userId))
        {
            await notifications.EnqueueAsync(
                NotificationEvents.ReviewReceived,
                owner.Id,
                owner.LineUserId,
                new
                {
                    providerId,
                    rating = review.Rating,
                    reviewType = review.ReviewType.ToString(),
                    serviceRequestId = review.ServiceRequestId,
                },
                cancellationToken);
        }

        return review;
    }

    public async Task<IReadOnlyList<Review>> ListForProviderAsync(Guid providerId, CancellationToken cancellationToken) =>
        await db.Reviews.AsNoTracking()
            .Where(r => r.ProviderId == providerId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    private async Task RecalculateProviderRatingsAsync(Provider provider, CancellationToken cancellationToken)
    {
        var verified = await db.Reviews.Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Verified && r.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);
        var pub = await db.Reviews.Where(r => r.ProviderId == provider.Id && r.ReviewType == ReviewType.Public && r.Status == ReviewStatus.Published)
            .ToListAsync(cancellationToken);

        provider.VerifiedReviewCount = verified.Count;
        provider.PublicReviewCount = pub.Count;
        provider.AverageVerifiedRating = verified.Count == 0 ? 0 : (decimal)verified.Average(r => r.Rating);
        provider.AveragePublicRating = pub.Count == 0 ? 0 : (decimal)pub.Average(r => r.Rating);
        provider.UpdatedAt = DateTime.UtcNow;
    }
}
