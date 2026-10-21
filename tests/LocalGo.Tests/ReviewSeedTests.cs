using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Tests;

public sealed class ReviewSeedTests
{
    [Fact]
    public async Task SeedQaProvider01SampleReviews_sets_average_4_5_with_two_reviews()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var provUser = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"{AdminAppService.TestUserLinePrefix}prov-01",
            DisplayName = "QA ผู้ให้ 01",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var req1 = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"{AdminAppService.TestUserLinePrefix}req-01",
            DisplayName = "QA ผู้รับ 01",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var req2 = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"{AdminAppService.TestUserLinePrefix}req-02",
            DisplayName = "QA ผู้รับ 02",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = provUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "QA ผู้ให้ 01",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.AddRange(provUser, req1, req2);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var reviews = new ReviewAppService(db, TestDbFactory.NoopNotifications());
        var seeded = await reviews.SeedQaProvider01SampleReviewsAsync(CancellationToken.None);

        Assert.Equal(2, seeded);
        var updated = await db.Providers.AsNoTracking().SingleAsync(p => p.Id == provider.Id);
        Assert.Equal(2, updated.VerifiedReviewCount);
        Assert.Equal(4.5m, updated.AverageVerifiedRating);
    }
}
