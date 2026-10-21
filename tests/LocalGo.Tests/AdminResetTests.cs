using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Tests;

public sealed class AdminResetTests
{
    [Fact]
    public async Task ResetTestDataAsync_removes_requests_and_keeps_persona_users()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"{AdminAppService.TestUserLinePrefix}req-reset",
            DisplayName = "Reset Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var providerUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"{AdminAppService.TestUserLinePrefix}prov-reset",
            DisplayName = "Reset Provider",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Reset Cat",
            Slug = $"reset-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = providerUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Reset Co",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Reset job",
            Status = ServiceRequestStatus.Open,
            Latitude = 13.7,
            Longitude = 100.5,
            Location = GeoHelper.CreatePoint(13.7, 100.5),
            SearchRadiusMeters = 5000,
            Source = ServiceRequestSource.Marketplace,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var review = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerUserId = requester.Id,
            ProviderId = provider.Id,
            ServiceRequestId = request.Id,
            ReviewType = ReviewType.Verified,
            Rating = 5,
            Status = ReviewStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Users.AddRange(requester, providerUser);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ServiceRequests.Add(request);
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        var admin = new AdminAppService(db, new ReviewAppService(db, TestDbFactory.NoopNotifications()));
        await admin.ResetTestDataAsync(CancellationToken.None);
        Assert.Equal(2, await db.Users.CountAsync(u => u.LineUserId.StartsWith(AdminAppService.TestUserLinePrefix)));
        Assert.Equal(0, await db.ServiceRequests.CountAsync(r => r.Id == request.Id));
        Assert.Equal(0, await db.Reviews.CountAsync(r => r.Id == review.Id));
    }
}
