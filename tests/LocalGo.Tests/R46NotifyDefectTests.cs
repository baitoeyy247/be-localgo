using System.Text.Json;
using LocalGo.Application.Abstractions.Notifications;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using LocalGo.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace LocalGo.Tests;

/// <summary>R46: notification defects D1–D8 from the R45 static review.</summary>
public sealed class R46NotifyDefectTests
{
    private static (INotificationPublisher pub, List<(string evt, Guid user, string json)> calls) Recording(Func<string, bool>? throwFor = null)
    {
        var calls = new List<(string, Guid, string)>();
        var pub = Substitute.For<INotificationPublisher>();
        pub.EnqueueAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var evt = ci.ArgAt<string>(0);
                calls.Add((evt, ci.ArgAt<Guid>(1), JsonSerializer.Serialize(ci.ArgAt<object?>(3))));
                if (throwFor?.Invoke(evt) == true) throw new InvalidOperationException("boom");
                return Task.CompletedTask;
            });
        return (pub, calls);
    }

    private static INotificationSender OkSender(List<NotificationSendRequest> sent)
    {
        var s = Substitute.For<INotificationSender>();
        s.SendAsync(Arg.Any<NotificationSendRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci => { sent.Add(ci.ArgAt<NotificationSendRequest>(0)); return Task.FromResult(new NotificationSendResult(NotificationSendOutcome.Sent)); });
        return s;
    }

    // ---- D1 / D2: shared recipient filter (covers console sender + outbox) ----
    [Theory]
    [InlineData(UserStatus.Active, "X", null)]
    [InlineData(UserStatus.Suspended, "X", "Recipient suspended")]
    [InlineData(UserStatus.Suspended, NotificationEvents.AccountSuspended, null)]
    [InlineData(UserStatus.Deleted, "X", "Recipient deleted")]
    [InlineData(UserStatus.Deleted, NotificationEvents.AccountSuspended, "Recipient deleted")]
    public void D1_D2_block_reason(UserStatus status, string evt, string? expected)
        => Assert.Equal(expected, NotificationRecipientFilter.BlockReason(status, evt));

    [Theory]
    [InlineData(UserStatus.Suspended)]
    [InlineData(UserStatus.Deleted)]
    public async Task D1_D2_publisher_skips_blocked_recipient_even_with_console_sender(UserStatus status)
    {
        await using var db = TestDbFactory.CreateContext();
        var user = new User { Id = Guid.NewGuid(), LineUserId = "Ux", DisplayName = "u", Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var sent = new List<NotificationSendRequest>();
        var sut = new NotificationPublisher(db, OkSender(sent), NullLogger<NotificationPublisher>.Instance);

        await sut.EnqueueAsync(NotificationEvents.NewMatchingRequest, user.Id, "Ux", new { }, CancellationToken.None);

        Assert.Empty(sent);
        var log = db.NotificationLogs.Single();
        Assert.Equal(NotificationStatus.Skipped, log.Status);
    }

    [Fact]
    public async Task D1_account_suspended_still_reaches_suspended_user()
    {
        await using var db = TestDbFactory.CreateContext();
        var user = new User { Id = Guid.NewGuid(), LineUserId = "Ux", DisplayName = "u", Status = UserStatus.Suspended, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var sent = new List<NotificationSendRequest>();
        var sut = new NotificationPublisher(db, OkSender(sent), NullLogger<NotificationPublisher>.Instance);

        await sut.EnqueueAsync(NotificationEvents.AccountSuspended, user.Id, "Ux", new { }, CancellationToken.None);

        Assert.Single(sent);
    }

    // ---- D6: claim ----
    [Fact]
    public async Task D6_fresh_sending_row_is_not_resent_but_stale_one_is()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var fresh = new NotificationLog { Id = Guid.NewGuid(), EventType = "X", LineUserId = "Ua", Status = NotificationStatus.Sending, ClaimedAt = now, CreatedAt = now.AddMinutes(-10) };
        var stale = new NotificationLog { Id = Guid.NewGuid(), EventType = "X", LineUserId = "Ub", Status = NotificationStatus.Sending, ClaimedAt = now.AddMinutes(-10), CreatedAt = now.AddMinutes(-10) };
        db.NotificationLogs.AddRange(fresh, stale);
        await db.SaveChangesAsync();
        var sent = new List<NotificationSendRequest>();
        var sut = new NotificationPublisher(db, OkSender(sent), NullLogger<NotificationPublisher>.Instance);

        await sut.RetryPendingAsync(CancellationToken.None);

        var one = Assert.Single(sent);
        Assert.Equal(stale.Id, one.LogId);
        Assert.Equal(NotificationStatus.Sending, db.NotificationLogs.Single(l => l.Id == fresh.Id).Status);
    }

    [Fact]
    public async Task D6_sent_row_is_not_sent_again_by_retry()
    {
        await using var db = TestDbFactory.CreateContext();
        var sent = new List<NotificationSendRequest>();
        var sut = new NotificationPublisher(db, OkSender(sent), NullLogger<NotificationPublisher>.Instance);
        await sut.EnqueueAsync("X", Guid.NewGuid(), "Ua", new { }, CancellationToken.None);
        await sut.RetryPendingAsync(CancellationToken.None);
        Assert.Single(sent);
    }

    // ---- D3 ----
    [Fact]
    public async Task D3_clear_selection_notifies_removed_provider()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        var (pub, calls) = Recording();
        var sut = new BidAppService(db, pub, NullLogger<BidAppService>.Instance);

        await sut.ClearSelectionAsync(seed.Requester.Id, seed.Request.Id, CancellationToken.None);

        var c = Assert.Single(calls, x => x.evt == NotificationEvents.SelectionCleared);
        Assert.Equal(seed.ProviderUser.Id, c.user);
    }

    // ---- D4 ----
    [Fact]
    public async Task D4_start_and_complete_notify_requester_once_each()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        seed.Appointment.ScheduledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var (pub, calls) = Recording();
        var sut = new ServiceRequestAppService(db, pub, NullLogger<ServiceRequestAppService>.Instance);

        await sut.StartAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None);
        await sut.CompleteAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None);

        var upd = calls.Where(x => x.evt == NotificationEvents.AppointmentUpdated).ToList();
        Assert.Equal(2, upd.Count);
        Assert.All(upd, x => Assert.Equal(seed.Requester.Id, x.user));
        Assert.Contains("InProgress", upd[0].json);
        Assert.Contains("Completed", upd[1].json);
    }

    // ---- D5 ----
    [Fact]
    public async Task D5_expiry_commits_and_notifies_requester_and_provider_even_if_one_send_throws()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        seed.Request.FirstBidAt = DateTime.UtcNow.AddDays(-30);
        seed.Request.LastProgressAt = DateTime.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();
        var first = true;
        var (pub, calls) = Recording(_ => { var t = first; first = false; return t; });
        var sut = new ServiceRequestAppService(db, pub, NullLogger<ServiceRequestAppService>.Instance);

        await sut.ExpireStaleAsync(CancellationToken.None);

        Assert.Equal(ServiceRequestStatus.Expired, db.ServiceRequests.Single(r => r.Id == seed.Request.Id).Status);
        var exp = calls.Where(x => x.evt == NotificationEvents.RequestExpired).ToList();
        Assert.Contains(exp, x => x.user == seed.Requester.Id);
        Assert.Contains(exp, x => x.user == seed.ProviderUser.Id && x.json.Contains("\"audience\":\"provider\""));
    }

    // ---- D7 ----
    [Fact]
    public async Task D7_noop_edit_sends_nothing_and_real_edit_says_edited()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        var a = seed.Appointment!;
        var (pub, calls) = Recording();
        var sut = new AppointmentAppService(db, pub, NullLogger<AppointmentAppService>.Instance);

        await sut.CreateAsync(seed.Requester.Id, seed.Request.Id, new CreateAppointmentRequest(a.ScheduledAt, a.AddressText, null, null, a.Note), CancellationToken.None);
        Assert.Empty(calls);

        await sut.CreateAsync(seed.Requester.Id, seed.Request.Id, new CreateAppointmentRequest(a.ScheduledAt!.Value.AddHours(1), a.AddressText, null, null, a.Note), CancellationToken.None);
        var c = Assert.Single(calls);
        Assert.Equal(NotificationEvents.AppointmentUpdated, c.evt);
        Assert.Contains("\"changeKind\":\"edited\"", c.json);
    }

    [Fact]
    public async Task D7_edited_title_not_confirmed()
    {
        await using var db = TestDbFactory.CreateContext();
        var (alt, _) = await NotificationMessageBuilder.BuildAsync(
            NotificationEvents.AppointmentUpdated,
            JsonDocument.Parse("""{"status":"Confirmed","changeKind":"edited","actorRole":"requester"}""").RootElement,
            "", db, null, CancellationToken.None);
        Assert.Contains("ลูกค้าแก้ไขรายละเอียดนัดหมาย", alt);
    }

    // ---- D8 ----
    [Fact]
    public async Task D8_recently_announced_bid_is_not_reannounced()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedProviderSelectedBidAsync(db);
        db.NotificationLogs.Add(new NotificationLog
        {
            Id = Guid.NewGuid(), EventType = NotificationEvents.NewBidReceived, UserId = seed.Requester.Id,
            PayloadJson = $"{{\"bidId\":\"{seed.RejectedBid.Id}\"}}", Status = NotificationStatus.Sent, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var sut = new BidAppService(db, TestDbFactory.NoopNotifications(), NullLogger<BidAppService>.Instance);
        Assert.True(await sut.RecentlyAnnouncedAsync(seed.RejectedBid.Id, seed.Requester.Id));
        Assert.False(await sut.RecentlyAnnouncedAsync(seed.Bid.Id, seed.Requester.Id));
    }
}
