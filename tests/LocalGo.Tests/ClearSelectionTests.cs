using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class ClearSelectionTests
{
    [Fact]
    public async Task ClearSelectionAsync_reverts_bids_and_request_to_HasOffers()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        var sut = new BidAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<BidAppService>.Instance);

        await sut.ClearSelectionAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None);

        var request = await db.ServiceRequests.FindAsync(seed.Request.Id);
        var selectedBid = await db.Bids.FindAsync(seed.Bid.Id);
        var rejectedBid = await db.Bids.FindAsync(seed.RejectedBid.Id);

        Assert.NotNull(request);
        Assert.Equal(ServiceRequestStatus.HasOffers, request!.Status);
        Assert.Null(request.SelectedBidId);
        Assert.Null(request.SelectedProviderId);
        Assert.Equal(BidStatus.Submitted, selectedBid!.Status);
        Assert.Equal(BidStatus.Submitted, rejectedBid!.Status);
    }

    [Fact]
    public async Task ClearSelectionAsync_cancels_pending_appointment()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new BidAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<BidAppService>.Instance);

        await sut.ClearSelectionAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None);

        var appointment = await db.Appointments.FindAsync(seed.Appointment!.Id);
        Assert.NotNull(appointment);
        Assert.Equal(AppointmentStatus.Cancelled, appointment!.Status);
    }

    [Fact]
    public async Task ClearSelectionAsync_blocks_when_appointment_confirmed()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync();

        var sut = new BidAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<BidAppService>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ClearSelectionAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ClearSelectionAsync_returns_not_found_for_non_requester()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        var sut = new BidAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<BidAppService>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.ClearSelectionAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None));

        Assert.Equal(404, ex.StatusCode);
    }
}
