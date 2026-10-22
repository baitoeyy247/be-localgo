using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class Qa27PerformanceTests
{
    [Fact]
    public async Task GetDetailAsync_requester_loads_many_bids_without_N_plus_one_failure()
    {
        await using var db = TestDbFactory.CreateContext();
        var (requesterId, requestId, _) = await SeedRequestWithManyBidsAsync(db, bidCount: 12);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(requesterId, requestId, isAdmin: false, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(12, detail!.Bids.Count);
        Assert.All(detail.Bids, b => Assert.NotNull(b.Provider));
    }

    [Fact]
    public async Task GetDetailAsync_provider_sees_only_own_bid()
    {
        await using var db = TestDbFactory.CreateContext();
        var (_, requestId, providerUserId) = await SeedRequestWithManyBidsAsync(
            db, bidCount: 5, trackProviderUserIndex: 2);
        Assert.NotNull(providerUserId);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(providerUserId.Value, requestId, isAdmin: false, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Single(detail!.Bids);
        Assert.Equal(providerUserId.Value, detail.Bids.First().Provider!.OwnerUserId);
    }

    [Fact]
    public async Task ListMatchingForProviderAsync_still_returns_engagements_with_bid_counts()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var inbox = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: true,
            CancellationToken.None);

        var row = Assert.Single(inbox.MyJobs!, x => x.Id == seed.Request.Id);
        Assert.True(row.BidCount >= 1);
    }

    [Fact]
    public async Task Appointment_GetByIdAsync_loads_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var appointment = await sut.GetByIdAsync(
            seed.Requester.Id,
            seed.Appointment!.Id,
            CancellationToken.None);

        Assert.NotNull(appointment.Provider);
    }

    private static async Task<(Guid RequesterId, Guid RequestId, Guid? ProviderUserId)> SeedRequestWithManyBidsAsync(
        LocalGoDbContext db,
        int bidCount,
        int? trackProviderUserIndex = null)
    {
        var now = DateTime.UtcNow;
        var requester = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-req-perf-{Guid.NewGuid():N}",
            DisplayName = "Perf Requester",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Perf",
            Slug = $"perf-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            CategoryId = category.Id,
            Title = "Perf detail",
            Description = "x",
            AddressText = "Bangkok",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 5000,
            BudgetText = "1000",
            Status = ServiceRequestStatus.HasOffers,
            Source = ServiceRequestSource.Marketplace,
            CreatedAt = now,
            UpdatedAt = now,
            LastProgressAt = now,
        };

        db.Users.Add(requester);
        db.ServiceCategories.Add(category);
        db.ServiceRequests.Add(request);

        Guid? trackedProviderUserId = null;
        for (var i = 0; i < bidCount; i++)
        {
            var owner = new User
            {
                Id = Guid.NewGuid(),
                LineUserId = $"line-bid-{i}-{Guid.NewGuid():N}",
                DisplayName = $"Bidder {i}",
                CreatedAt = now,
                UpdatedAt = now,
            };
            var provider = new Provider
            {
                Id = Guid.NewGuid(),
                OwnerUserId = owner.Id,
                ProviderType = ProviderType.Individual,
                Name = $"Provider {i}",
                Status = ProviderStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            };
            if (trackProviderUserIndex == i)
            {
                trackedProviderUserId = owner.Id;
            }

            db.Users.Add(owner);
            db.Providers.Add(provider);
            db.Bids.Add(new Bid
            {
                Id = Guid.NewGuid(),
                ServiceRequestId = request.Id,
                ProviderId = provider.Id,
                Amount = 100 + i,
                Currency = "THB",
                PriceText = (100 + i).ToString(),
                Description = "bid",
                Status = BidStatus.Submitted,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return (requester.Id, request.Id, trackedProviderUserId);
    }
}
