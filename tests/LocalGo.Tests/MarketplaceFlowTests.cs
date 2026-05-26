using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

/// <summary>
/// Critical API flows: provider matching (active jobs + needsAppointmentConfirm),
/// GetByRequestIdAsync for provider viewer, GetDetailAsync provider access.
/// E2E: scripts/smoke-appointment-flow.sh
/// </summary>
public sealed class MarketplaceFlowTests
{
    [Fact]
    public async Task ListMatchingForProvider_includes_scheduled_job_with_needsAppointmentConfirm()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var items = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: true,
            CancellationToken.None);

        var row = Assert.Single(items, x => x.Id == seed.Request.Id);
        Assert.True(row.NeedsAppointmentConfirm);
        Assert.Equal(ServiceRequestStatus.Scheduled.ToString(), row.Status);
    }

    [Fact]
    public async Task ListMatchingForProvider_includes_direct_booking_pending_confirm()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedDirectBookingAsync(db);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var items = await sut.ListMatchingForProviderAsync(
            seed.ProviderUser.Id,
            excludeSmokeTestData: false,
            includeOwnRequests: true,
            CancellationToken.None);

        var row = Assert.Single(items, x => x.Id == seed.Request.Id);
        Assert.True(row.NeedsAppointmentConfirm);
        Assert.Equal(ServiceRequestStatus.ProviderSelected.ToString(), row.Status);
    }

    [Fact]
    public async Task GetByRequestIdAsync_allows_provider_viewer()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var appointment = await sut.GetByRequestIdAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            CancellationToken.None);

        Assert.NotNull(appointment);
        Assert.Equal(seed.Appointment!.Id, appointment!.Id);
        Assert.Equal(AppointmentStatus.Pending, appointment.Status);
    }

    [Fact]
    public async Task GetDetailAsync_allows_selected_provider_without_bid_on_direct_booking()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedDirectBookingAsync(db);
        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            isAdmin: false,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(seed.Request.Id, detail!.Id);
        Assert.Empty(detail.Bids);
    }

    [Fact]
    public async Task GetDetailAsync_denies_unrelated_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var otherProviderUser = new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"other-{Guid.NewGuid():N}",
            DisplayName = "Other",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        var otherProvider = new Domain.Entities.Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = otherProviderUser.Id,
            ProviderType = ProviderType.Individual,
            Name = "Other Co",
            Status = ProviderStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(otherProviderUser);
        db.Providers.Add(otherProvider);
        await db.SaveChangesAsync();

        var sut = new ServiceRequestAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<ServiceRequestAppService>.Instance);

        var detail = await sut.GetDetailAsync(
            otherProviderUser.Id,
            seed.Request.Id,
            isAdmin: false,
            CancellationToken.None);

        Assert.Null(detail);
    }
}
