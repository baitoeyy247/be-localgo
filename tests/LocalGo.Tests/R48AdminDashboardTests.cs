using System.Text.Json;
using LocalGo.Api.Controllers;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace LocalGo.Tests;

/// <summary>R48 admin dashboard: A-2 recalculate is non-destructive, A-3 notifications list has no N+1.</summary>
public sealed class R48AdminDashboardTests
{
    /// <summary>Counts DbSet accesses — each LINQ query against the DB starts with exactly one.</summary>
    private sealed class CountingDb(LocalGoDbContext inner) : ILocalGoDbContext
    {
        public Dictionary<string, int> Hits { get; } = new();
        private T Hit<T>(string name, T set) { Hits[name] = Hits.GetValueOrDefault(name) + 1; return set; }
        public int Total => Hits.Values.Sum();
        public DbSet<User> Users => Hit(nameof(Users), inner.Users);
        public DbSet<Provider> Providers => Hit(nameof(Providers), inner.Providers);
        public DbSet<ProviderBranch> ProviderBranches => Hit(nameof(ProviderBranches), inner.ProviderBranches);
        public DbSet<ServiceCategory> ServiceCategories => Hit(nameof(ServiceCategories), inner.ServiceCategories);
        public DbSet<ProviderService> ProviderServices => Hit(nameof(ProviderServices), inner.ProviderServices);
        public DbSet<ProviderServiceBranch> ProviderServiceBranches => Hit(nameof(ProviderServiceBranches), inner.ProviderServiceBranches);
        public DbSet<ServiceRequest> ServiceRequests => Hit(nameof(ServiceRequests), inner.ServiceRequests);
        public DbSet<Bid> Bids => Hit(nameof(Bids), inner.Bids);
        public DbSet<Appointment> Appointments => Hit(nameof(Appointments), inner.Appointments);
        public DbSet<Review> Reviews => Hit(nameof(Reviews), inner.Reviews);
        public DbSet<NotificationLog> NotificationLogs => Hit(nameof(NotificationLogs), inner.NotificationLogs);
        public DbSet<Report> Reports => Hit(nameof(Reports), inner.Reports);
        public DbSet<AdminActionLog> AdminActionLogs => Hit(nameof(AdminActionLogs), inner.AdminActionLogs);
        public DbSet<MediaAsset> MediaAssets => Hit(nameof(MediaAssets), inner.MediaAssets);
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);
        public void ClearChangeTracker() => inner.ChangeTracker.Clear();
    }

    private static AdminController Controller(LocalGoDbContext ef, ILocalGoDbContext db, string env = "Staging")
    {
        var admin = new AdminAppService(ef, new ReviewAppService(ef, TestDbFactory.NoopNotifications()), environmentName: env);
        return new AdminController(admin, db, ef, Substitute.For<LocalGo.Application.Abstractions.Auth.ICurrentUserService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static async Task SeedNotificationsAsync(LocalGoDbContext db, TestDbFactory.SeededMarketplaceJob seed, int count)
    {
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            // each row points at its own request so the old code would issue one query per row
            var req = new ServiceRequest
            {
                Id = Guid.NewGuid(),
                RequesterUserId = seed.Requester.Id,
                CategoryId = seed.Request.CategoryId,
                Title = $"Job {i}",
                Latitude = 13.75, Longitude = 100.5,
                Location = LocalGo.Application.Common.GeoHelper.CreatePoint(13.75, 100.5),
                SearchRadiusMeters = 5000,
                Status = ServiceRequestStatus.Open,
                Source = ServiceRequestSource.Marketplace,
                LastProgressAt = now, CreatedAt = now, UpdatedAt = now,
            };
            db.ServiceRequests.Add(req);
            db.NotificationLogs.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                UserId = seed.ProviderUser.Id,
                EventType = i % 2 == 0 ? NotificationEvents.AppointmentUpdated : NotificationEvents.NewBidReceived,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    requestId = req.Id.ToString(),
                    categoryId = req.CategoryId.ToString(),
                    status = "Confirmed",
                    providerName = "P",
                    priceText = "500",
                }),
                Status = NotificationStatus.Sent,
                CreatedAt = now.AddSeconds(-i),
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<(int dbHits, List<JsonElement> rows)> ListNotificationsAsync(int count)
    {
        await using var ef = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(ef);
        await SeedNotificationsAsync(ef, seed, count);
        var counting = new CountingDb(ef);
        var result = await Controller(ef, counting).Notifications(page: 1, pageSize: 100);
        var ok = Assert.IsType<OkObjectResult>(result);
        var rows = JsonSerializer.Deserialize<List<JsonElement>>(JsonSerializer.Serialize(ok.Value))!;
        return (counting.Total, rows);
    }

    [Fact]
    public async Task A3_notifications_query_count_is_bounded_regardless_of_row_count()
    {
        var (few, fewRows) = await ListNotificationsAsync(3);
        var (many, manyRows) = await ListNotificationsAsync(40);

        Assert.Equal(3, fewRows.Count);
        Assert.Equal(40, manyRows.Count);
        Assert.Equal(few, many);
        // NotificationLogs (count + page share one IQueryable) + ServiceRequests batch + ServiceCategories batch
        Assert.True(many <= 3, $"expected ≤3 DbSet queries, got {many}");
        // previews still rendered from the batch
        Assert.All(manyRows, r => Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("title").GetString())));
        Assert.Contains(manyRows, r => (r.GetProperty("body").GetString() ?? "").Contains("Test Category"));
    }

    [Fact]
    public async Task A3_batched_preview_matches_per_row_builder()
    {
        await using var ef = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(ef);
        await SeedNotificationsAsync(ef, seed, 4);
        var logs = await ef.NotificationLogs.AsNoTracking().ToListAsync();
        var payloads = logs.Select(l => (JsonElement?)JsonDocument.Parse(l.PayloadJson!).RootElement).ToList();
        var lookup = await LocalGo.Infrastructure.Notifications.NotificationLookup.LoadAsync(payloads, ef, CancellationToken.None);
        for (var i = 0; i < logs.Count; i++)
        {
            var single = await LocalGo.Infrastructure.Notifications.NotificationMessageBuilder.BuildAsync(
                logs[i].EventType, payloads[i], "https://liff/x", ef, logs[i].UserId, CancellationToken.None);
            var batched = await LocalGo.Infrastructure.Notifications.NotificationMessageBuilder.BuildAsync(
                logs[i].EventType, payloads[i], "https://liff/x", ef, logs[i].UserId, CancellationToken.None, lookup);
            Assert.Equal(single.altText, batched.altText);
            Assert.Equal(JsonSerializer.Serialize(single.message), JsonSerializer.Serialize(batched.message));
        }
    }

    [Fact]
    public async Task A2_recalculate_ratings_removes_no_rows_with_dev_gate_off()
    {
        await using var ef = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(ef, withPendingAppointment: true);
        var now = DateTime.UtcNow;
        // rows RepairSitPlaytestDataAsync would have deleted: automated dev user + persona
        ef.Users.Add(new User { Id = Guid.NewGuid(), LineUserId = "dev-apptflow-1", DisplayName = "Appt Flow tester", CreatedAt = now, UpdatedAt = now });
        ef.Users.Add(new User { Id = Guid.NewGuid(), LineUserId = $"{AdminAppService.TestUserLinePrefix}req-01", DisplayName = "QA 01", CreatedAt = now, UpdatedAt = now });
        ef.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(), ReviewerUserId = seed.Requester.Id, ProviderId = seed.Provider.Id,
            ServiceRequestId = seed.Request.Id, ReviewType = ReviewType.Verified, Rating = 4,
            Status = ReviewStatus.Published, CreatedAt = now, UpdatedAt = now,
        });
        await ef.SaveChangesAsync();

        async Task<int[]> Counts() => new[]
        {
            await ef.Users.CountAsync(), await ef.ServiceRequests.CountAsync(), await ef.Bids.CountAsync(),
            await ef.Appointments.CountAsync(), await ef.Reviews.CountAsync(), await ef.Providers.CountAsync(),
            await ef.NotificationLogs.CountAsync(),
        };
        var before = await Counts();

        // the endpoint no longer reads the dev gate at all; ALLOW_DEV_LOGIN is unset (gate off) here
        var result = await Controller(ef, ef).RecalculateRatings(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        ef.ChangeTracker.Clear();
        Assert.Equal(before, await Counts());
        var provider = await ef.Providers.AsNoTracking().SingleAsync(p => p.Id == seed.Provider.Id);
        Assert.Equal(1, provider.VerifiedReviewCount);
    }

    [Fact]
    public async Task A3_dashboard_counts_unchanged_semantics_in_bounded_queries()
    {
        await using var ef = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(ef); // 1 Scheduled request
        await SeedNotificationsAsync(ef, seed, 2); // +2 Open requests, 2 Sent logs
        var now = DateTime.UtcNow;
        foreach (var st in new[] { ServiceRequestStatus.Completed, ServiceRequestStatus.Expired, ServiceRequestStatus.Expired, ServiceRequestStatus.Draft, ServiceRequestStatus.Cancelled })
        {
            ef.ServiceRequests.Add(new ServiceRequest
            {
                Id = Guid.NewGuid(), RequesterUserId = seed.Requester.Id, CategoryId = seed.Request.CategoryId,
                Title = st.ToString(), Latitude = 1, Longitude = 1, Location = LocalGo.Application.Common.GeoHelper.CreatePoint(1, 1),
                SearchRadiusMeters = 1, Status = st, Source = ServiceRequestSource.Marketplace,
                LastProgressAt = now, CreatedAt = now, UpdatedAt = now,
            });
        }
        ef.NotificationLogs.Add(new NotificationLog { Id = Guid.NewGuid(), EventType = "X", Status = NotificationStatus.Pending, CreatedAt = now });
        await ef.SaveChangesAsync();

        var counting = new CountingDb(ef);
        var admin = new AdminAppService(counting, new ReviewAppService(ef, TestDbFactory.NoopNotifications()), environmentName: "Staging");
        var d = await admin.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(2, d.Users);
        Assert.Equal(1, d.Providers);
        Assert.Equal(3, d.PublishedRequests); // Scheduled + 2 Open
        Assert.Equal(1, d.CompletedRequests);
        Assert.Equal(2, d.ExpiredRequests);
        Assert.Equal(1, d.PendingNotifications);
        Assert.True(counting.Total <= 4, $"expected ≤4 queries, got {counting.Total}");
    }
}
