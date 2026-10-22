using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class Qa26WithdrawBidTests
{
    [Fact]
    public async Task WithdrawAsync_submitted_bid_sets_withdrawn()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenBidRequestAsync(db);
        var sut = new BidAppService(db, TestDbFactory.NoopNotifications(), NullLogger<BidAppService>.Instance);

        var bid = await sut.SubmitAsync(
            seed.ProviderUser.Id,
            seed.Request.Id,
            new SubmitBidRequest("600", 600, null, null, null),
            CancellationToken.None);

        await sut.WithdrawAsync(seed.ProviderUser.Id, bid.Id, CancellationToken.None);

        var updated = await db.Bids.FindAsync(bid.Id);
        Assert.NotNull(updated);
        Assert.Equal(BidStatus.Withdrawn, updated!.Status);
    }

    [Fact]
    public async Task WithdrawAsync_rejects_non_submitted_status_with_thai_message()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: false);
        var sut = new BidAppService(db, TestDbFactory.NoopNotifications(), NullLogger<BidAppService>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.WithdrawAsync(seed.ProviderUser.Id, seed.Bid.Id, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(GlossaryMessages.WithdrawBidInvalidStatus, ex.Message);
    }

    [Fact]
    public async Task UpdateAppointmentStatus_by_requester_cancels_pending_appointment()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var sut = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var updated = await sut.UpdateStatusAsync(
            seed.Requester.Id,
            seed.Appointment!.Id,
            AppointmentStatus.Cancelled,
            null,
            CancellationToken.None);

        Assert.Equal(AppointmentStatus.Cancelled, updated.Status);
    }
}
